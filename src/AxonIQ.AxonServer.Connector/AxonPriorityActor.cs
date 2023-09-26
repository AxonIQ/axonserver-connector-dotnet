using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AxonPriorityActor<TMessage, TState> : IAxonPriorityActor<TMessage>, IAsyncDisposable
{
    private readonly Func<TMessage, TState, CancellationToken, Task<TState>> _receiver;
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Channel<(TMessage, MessagePriority)> _inbox;
    private readonly Task _consumer;
    
    private TState _state;
    private long _disposed;

    public AxonPriorityActor(
        Func<TMessage, TState, CancellationToken, Task<TState>> receiver, 
        TState initialState,
        IScheduler scheduler, 
        ILogger logger)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _state = initialState;
        
        _inboxCancellation = new CancellationTokenSource();
        
        _inbox = PriorityChannel<TMessage, MessagePriority>.CreateUnbounded(
            new UnboundedChannelOptions
            {
                SingleWriter = false, 
                SingleReader = true, 
                AllowSynchronousContinuations = false // TODO: Figure out if this needs to be false or true
            });

        _consumer = ConsumeInbox(_inboxCancellation.Token);
    }

    private async Task ConsumeInbox(CancellationToken ct)
    {
        try
        {
            while (await _inbox.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_inbox.Reader.TryRead(out var item))
                {
                    if (item.Item1 == null) continue;
                    
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Received {Message} as {Priority} when {State}", item.Item1.ToString(), item.Item2.ToString(), _state!.ToString());
                    }

                    _state = await _receiver(item.Item1, _state, ct);
                    
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Completed {Message} as {Priority} with {State}", item.Item1.ToString(), item.Item2.ToString(), _state!.ToString());
                    }
                }
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Actor protocol loop is exciting gracefully");
            }
        }
        catch (OperationCanceledException exception)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(exception,
                    $"Actor protocol loop is exciting gracefully because an operation was cancelled");    
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Actor protocol loop is exciting unexpectedly because of an exception");
        }
    }

    public Func<DateTimeOffset> Clock => _scheduler.Clock;

    public TState State => _state;

    public ValueTask ScheduleAsync(TMessage message, TimeSpan due)
    {
        ThrowIfDisposed();
        return _scheduler.ScheduleTaskAsync(() => TellAsync(MessagePriority.Primary, message, _inboxCancellation.Token), due);
    }

    public ValueTask ScheduleAsync(TMessage message, TimeSpan due, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _scheduler.ScheduleTaskAsync(() => TellAsync(MessagePriority.Primary, message, ct), due);
    }
    
    public ValueTask ScheduleAsync(MessagePriority priority, TMessage message, TimeSpan due)
    {
        ThrowIfDisposed();
        return _scheduler.ScheduleTaskAsync(() => TellAsync(priority, message, _inboxCancellation.Token), due);
    }

    public ValueTask ScheduleAsync(MessagePriority priority, TMessage message, TimeSpan due, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _scheduler.ScheduleTaskAsync(() => TellAsync(priority, message, ct), due);
    }
    
    public bool TryTell(TMessage message)
    {
        ThrowIfDisposed();
        return TryTell(MessagePriority.Primary, message);
    }
    
    public bool TryTell(MessagePriority priority, TMessage message)
    {
        ThrowIfDisposed();
        return _inbox.Writer.TryWrite((message, priority));
    }

    public ValueTask TellAsync(TMessage message)
    {
        ThrowIfDisposed();
        return TellAsync(MessagePriority.Primary, message, _inboxCancellation.Token);
    }

    public ValueTask TellAsync(TMessage message, CancellationToken ct)
    {
        ThrowIfDisposed();
        return TellAsync(MessagePriority.Primary, message, ct);
    }
    
    public ValueTask TellAsync(MessagePriority priority, TMessage message)
    {
        ThrowIfDisposed();
        return TellAsync(priority, message, _inboxCancellation.Token);
    }

    public ValueTask TellAsync(MessagePriority priority, TMessage message, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _inbox.Writer.WriteAsync((message, priority), ct);
    }

    public ValueTask TellAsync(IReadOnlyCollection<TMessage> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();
        
        return TellAsync(MessagePriority.Primary, messages, _inboxCancellation.Token);
    }

    public ValueTask TellAsync(IReadOnlyCollection<TMessage> messages, CancellationToken ct)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();

        return TellAsync(MessagePriority.Primary, messages, ct);
    }
    
    public ValueTask TellAsync(MessagePriority priority, IReadOnlyCollection<TMessage> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();
        
        return TellAsync(priority, messages, _inboxCancellation.Token);
    }

    public async ValueTask TellAsync(MessagePriority priority, IReadOnlyCollection<TMessage> messages, CancellationToken ct)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();

        foreach (var message in messages)
        {
            await TellAsync(priority, message, ct);
        }
    }

    public CancellationToken CancellationToken => _inboxCancellation.Token; 

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes) throw new ObjectDisposedException(nameof(AxonActor<TMessage, TState>));
    }
    
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            _inboxCancellation.Cancel();
            _inbox.Writer.Complete();
            await _consumer.ConfigureAwait(false);
            _inboxCancellation.Dispose();
            _consumer.Dispose();
        }
    }
}