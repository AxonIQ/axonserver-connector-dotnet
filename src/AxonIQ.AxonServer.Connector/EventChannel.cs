using Google.Protobuf.WellKnownTypes;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class EventChannel : IEventChannel, IDisposable
{
    private readonly AxonServerConnection _connection;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EventChannel> _logger;
    
    private CancellationTokenSource? _cancellation;
    private long _disposed;

    public EventChannel(
        AxonServerConnection connection,
        Func<DateTimeOffset> clock,
        ILoggerFactory loggerFactory)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _logger = loggerFactory.CreateLogger<EventChannel>();
        _cancellation = new CancellationTokenSource();

        EventStore = new EventStore.EventStoreClient(connection.CallInvoker);
        EventScheduler = new EventScheduler.EventSchedulerClient(connection.CallInvoker);
    }
    
    public EventStore.EventStoreClient EventStore { get; }
    public EventScheduler.EventSchedulerClient EventScheduler { get; }

    internal void Reconnect()
    {
        _logger.LogDebug("Reconnect received");
        // Reading `_disposed` here is purely an optimization, fail fast.
        if (Interlocked.Read(ref _disposed) == Disposed.No)
        {
            // `CancellationTokenSource` can not be reset after being cancelled,
            // so we need a `next` instance for future cooperative cancellation.
            var next = new CancellationTokenSource();
            // This instance might have been disposed between reading `_disposed` and `_cancellation`.
            var previous = Interlocked.Exchange(ref _cancellation, next);
            if (previous != null)
            {
                _logger.LogDebug("Cancelling previous source {HashCode}", previous.GetHashCode());
                // We might be in a race with `Dispose()` here.
                // Trying to `Cancel` a disposed `CancellationTokenSource` may throw.
                try { previous.Cancel(); } catch(ObjectDisposedException) {}
                previous.Dispose();
            }
            else
            {
                _logger.LogDebug("Cancelling next source {HashCode}", next.GetHashCode());
                // We lost the race with `Dispose()` here, because previous is `null`, something only `Dispose()` does.
                // The `next` instance must be cleaned up here, since we have no idea what may have used it and it is
                // the last possible moment.
                next.Cancel();
                next.Dispose();
            }
        }
    }
    
    public IAppendEventsTransaction StartAppendEventsTransaction()
    {
        ThrowIfDisposed();
        return new AppendEventsTransaction(EventStore.AppendEvent(),
            _loggerFactory.CreateLogger<AppendEventsTransaction>());
    }

    public Task<ScheduledEventCancellationToken> ScheduleEventAsync(Duration duration, Event @event)
    {
        ThrowIfDisposed();
        return ScheduleEventAsync(_clock().Add(duration.ToTimeSpan()), @event);
    }

    public async Task<ScheduledEventCancellationToken> ScheduleEventAsync(DateTimeOffset instant, Event @event)
    {
        ThrowIfDisposed();
        var request = new ScheduleEventRequest
        {
            Instant = instant.ToUnixTimeMilliseconds(),
            Event = @event
        };
        using var call = EventScheduler.ScheduleEventAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new ScheduledEventCancellationToken(response.Token);
    }

    public async Task<InstructionAck> CancelScheduleAsync(ScheduledEventCancellationToken token)
    {
        ThrowIfDisposed();
        var request = new CancelScheduledEventRequest
        {
            Token = token.ToString()
        };
        using var call = EventScheduler.CancelScheduledEventAsync(request);
        return await call.ResponseAsync.ConfigureAwait(false);
    }

    public Task<ScheduledEventCancellationToken> RescheduleAsync(ScheduledEventCancellationToken token, Duration duration, Event @event)
    {
        ThrowIfDisposed();
        return RescheduleAsync(token, _clock().Add(duration.ToTimeSpan()), @event);
    }

    public async Task<ScheduledEventCancellationToken> RescheduleAsync(ScheduledEventCancellationToken token, DateTimeOffset instant, Event @event)
    {
        ThrowIfDisposed();
        var request = new RescheduleEventRequest
        {
            Event = @event,
            Instant = instant.ToUnixTimeMilliseconds(),
            Token = token.ToString()
        };
        using var call = EventScheduler.RescheduleEventAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new ScheduledEventCancellationToken(response.Token);
    }

    public async Task<EventSequenceNumber> FindHighestSequenceAsync(AggregateId id)
    {
        ThrowIfDisposed();
        var request = new ReadHighestSequenceNrRequest{ AggregateId = id.ToString() };
        using var call = EventStore.ReadHighestSequenceNrAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new EventSequenceNumber(response.ToSequenceNr);
    }

    public IEventStream OpenStream(EventStreamToken token, PermitCount bufferSize, PermitCount? refillBatch = default, bool forceReadFromLeader = false)
    {
        ThrowIfDisposed();
        var initial = PermitCount.Max(new PermitCount(64), bufferSize);
        var threshold = refillBatch.HasValue ? PermitCount.Max(new PermitCount(16), PermitCount.Min(bufferSize, refillBatch.Value)) : PermitCount.Max(new PermitCount(16), bufferSize);
        var call = EventStore.ListEvents();
        return new EventStream(initial, threshold, forceReadFromLeader, token, call, _cancellation?.Token ?? CancellationToken.None);
    }

    public IAggregateEventStream OpenStream(AggregateId id, bool allowSnapshots = true)
    {
        ThrowIfDisposed();
        var request = new GetAggregateEventsRequest
        {
            AggregateId = id.ToString(),
            AllowSnapshots = allowSnapshots
        };
        var call = EventStore.ListAggregateEvents(request);
        return new AggregateEventStream(call, _cancellation?.Token ?? CancellationToken.None, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public IAggregateEventStream OpenStream(AggregateId id, EventSequenceNumber from, EventSequenceNumber? to = default)
    {
        ThrowIfDisposed();
        var request = new GetAggregateEventsRequest
        {
            AggregateId = id.ToString(),
            InitialSequence = from.ToInt64(),
            MaxSequence = to?.ToInt64() ?? 0L
        };
        var call = EventStore.ListAggregateEvents(request);
        return new AggregateEventStream(call, _cancellation?.Token ?? CancellationToken.None, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public async Task<Confirmation> AppendSnapshotAsync(Event snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        
        ThrowIfDisposed();
        using var call = EventStore.AppendSnapshotAsync(snapshot);
        return await call.ResponseAsync.ConfigureAwait(false);
    }

    public IAggregateEventStream LoadSnapshots(AggregateId id, EventSequenceNumber? from = default, EventSequenceNumber? to = default,
        int maxResults = 1)
    {
        ThrowIfDisposed();
        var request = new GetAggregateSnapshotsRequest
        {
            AggregateId = id.ToString(),
            InitialSequence = from?.ToInt64() ?? 0L,
            MaxSequence = to?.ToInt64() ?? 0L,
            MaxResults = maxResults
        };
        var call = EventStore.ListAggregateSnapshots(request);
        return new AggregateEventStream(call, _cancellation?.Token ?? CancellationToken.None, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public async Task<EventStreamToken> GetLastTokenAsync()
    {
        ThrowIfDisposed();
        var request = new GetLastTokenRequest();
        using var call = EventStore.GetLastTokenAsync(request);
        var token = await call.ResponseAsync.ConfigureAwait(false);
        return new EventStreamToken(Math.Max(token.Token, 0L) - 1L);
    }

    public async Task<EventStreamToken> GetFirstTokenAsync()
    {
        ThrowIfDisposed();
        var request = new GetFirstTokenRequest();
        using var call = EventStore.GetFirstTokenAsync(request);
        var token = await call.ResponseAsync.ConfigureAwait(false);
        return new EventStreamToken(Math.Max(token.Token, 0L) - 1L);
    }

    public async Task<EventStreamToken> GetTokenAtAsync(long instant)
    {
        ThrowIfDisposed();
        var request = new GetTokenAtRequest
        {
            Instant = instant
        };
        using var call = EventStore.GetTokenAtAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new EventStreamToken(Math.Max(response.Token, 0L) - 1L);
    }

    public IAsyncEnumerable<IEventQueryResultEntry> QueryEvents(string expression, bool liveStream)
    {
        ThrowIfDisposed();
        var call = EventStore.QueryEvents();
        return new EventQueryResponseStream(_connection.Context, expression, liveStream, false, call, _loggerFactory.CreateLogger<EventQueryResponseStream>());
    }

    public IAsyncEnumerable<IEventQueryResultEntry> QuerySnapshotEvents(string expression, bool liveStream)
    {
        ThrowIfDisposed();
        var call = EventStore.QueryEvents();
        return new EventQueryResponseStream(_connection.Context, expression, liveStream, true, call, _loggerFactory.CreateLogger<EventQueryResponseStream>());
    }
    
    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes)
            throw new ObjectDisposedException(nameof(EventChannel));
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            var source = Interlocked.Exchange(ref _cancellation, null);
            if (source != null)
            {
                source.Cancel();
                source.Dispose();    
            }
        }
    }
}