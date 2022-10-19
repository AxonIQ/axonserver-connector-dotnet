using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class EventChannel : IEventChannel
{
    private readonly ILoggerFactory _loggerFactory;

    public EventChannel(
        ClientIdentity clientIdentity,
        Context context,
        Func<DateTimeOffset> clock,
        CallInvoker callInvoker,
        ILoggerFactory loggerFactory)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Context = context;
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        EventStore = new EventStore.EventStoreClient(callInvoker);
        EventScheduler = new EventScheduler.EventSchedulerClient(callInvoker);
    }
    
    public ClientIdentity ClientIdentity { get; }
    public Context Context { get; }
    public Func<DateTimeOffset> Clock { get; }
    public EventStore.EventStoreClient EventStore { get; }
    public EventScheduler.EventSchedulerClient EventScheduler { get; }
    
    public IAppendEventsTransaction StartAppendEventsTransaction()
    {
        return new AppendEventsTransaction(EventStore.AppendEvent(),
            _loggerFactory.CreateLogger<AppendEventsTransaction>());
    }

    public Task<ScheduledEventCancellationToken> ScheduleEvent(Duration duration, Event @event)
    {
        return ScheduleEvent(Clock().Add(duration.ToTimeSpan()), @event);
    }

    public async Task<ScheduledEventCancellationToken> ScheduleEvent(DateTimeOffset instant, Event @event)
    {
        var request = new ScheduleEventRequest
        {
            Instant = instant.ToUnixTimeMilliseconds(),
            Event = @event
        };
        using var call = EventScheduler.ScheduleEventAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new ScheduledEventCancellationToken(response.Token);
    }

    public async Task<InstructionAck> CancelSchedule(ScheduledEventCancellationToken token)
    {
        var request = new CancelScheduledEventRequest
        {
            Token = token.ToString()
        };
        using var call = EventScheduler.CancelScheduledEventAsync(request);
        return await call.ResponseAsync.ConfigureAwait(false);
    }

    public Task<ScheduledEventCancellationToken> Reschedule(ScheduledEventCancellationToken token, Duration duration, Event @event)
    {
        return Reschedule(token, Clock().Add(duration.ToTimeSpan()), @event);
    }

    public async Task<ScheduledEventCancellationToken> Reschedule(ScheduledEventCancellationToken token, DateTimeOffset instant, Event @event)
    {
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
        var request = new ReadHighestSequenceNrRequest{ AggregateId = id.ToString() };
        using var call = EventStore.ReadHighestSequenceNrAsync(request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        return new EventSequenceNumber(response.ToSequenceNr);
    }

    public async Task<IEventStream> OpenStreamAsync(EventStreamToken token, PermitCount bufferSize, PermitCount? refillBatch = default, bool forceReadFromLeader = false)
    {
        var initial = PermitCount.Max(new PermitCount(64), bufferSize);
        var threshold = refillBatch.HasValue ? PermitCount.Max(new PermitCount(16), PermitCount.Min(bufferSize, refillBatch.Value)) : PermitCount.Max(new PermitCount(16), bufferSize);
        var call = EventStore.ListEvents();
        await call.RequestStream.WriteAsync(new GetEventsRequest
        {
            ForceReadFromLeader = forceReadFromLeader,
            TrackingToken = token.ToInt64() + 1L,
            NumberOfPermits = initial.ToInt64()
        }).ConfigureAwait(false);
        return new EventStream(initial, threshold, call, _loggerFactory);
    }

    public IAggregateEventStream OpenStream(AggregateId id, bool allowSnapshots = true)
    {
        var request = new GetAggregateEventsRequest
        {
            AggregateId = id.ToString(),
            AllowSnapshots = allowSnapshots
        };
        var call = EventStore.ListAggregateEvents(request);
        return new AggregateEventStream(call, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public IAggregateEventStream OpenStream(AggregateId id, EventSequenceNumber from, EventSequenceNumber? to = default)
    {
        var request = new GetAggregateEventsRequest
        {
            AggregateId = id.ToString(),
            InitialSequence = from.ToInt64(),
            MaxSequence = to?.ToInt64() ?? 0L
        };
        var call = EventStore.ListAggregateEvents(request);
        return new AggregateEventStream(call, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public async Task<Confirmation> AppendSnapshotAsync(Event snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        using var call = EventStore.AppendSnapshotAsync(snapshot);
        return await call.ResponseAsync.ConfigureAwait(false);
    }

    public IAggregateEventStream LoadSnapshots(AggregateId id, EventSequenceNumber? from = default, EventSequenceNumber? to = default,
        int maxResults = 1)
    {
        var request = new GetAggregateSnapshotsRequest
        {
            AggregateId = id.ToString(),
            InitialSequence = from?.ToInt64() ?? 0L,
            MaxSequence = to?.ToInt64() ?? 0L,
            MaxResults = maxResults
        };
        var call = EventStore.ListAggregateSnapshots(request);
        return new AggregateEventStream(call, _loggerFactory.CreateLogger<AggregateEventStream>());
    }

    public async Task<EventStreamToken> GetLastToken()
    {
        var request = new GetLastTokenRequest();
        using var call = EventStore.GetLastTokenAsync(request);
        var token = await call.ResponseAsync.ConfigureAwait(false);
        return new EventStreamToken(Math.Max(token.Token, 0L) - 1L);
    }

    public async Task<EventStreamToken> GetFirstToken()
    {
        var request = new GetFirstTokenRequest();
        using var call = EventStore.GetFirstTokenAsync(request);
        var token = await call.ResponseAsync.ConfigureAwait(false);
        return new EventStreamToken(Math.Max(token.Token, 0L) - 1L);
    }

    public async Task<EventStreamToken> GetTokenAt(long instant)
    {
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
        var call = EventStore.QueryEvents();
        return new EventQueryResponseStream(expression, liveStream, false, call, _loggerFactory.CreateLogger<EventQueryResponseStream>());
    }

    public IAsyncEnumerable<IEventQueryResultEntry> QuerySnapshotEvents(string expression, bool liveStream)
    {
        var call = EventStore.QueryEvents();
        return new EventQueryResponseStream(expression, liveStream, true, call, _loggerFactory.CreateLogger<EventQueryResponseStream>());
    }
}