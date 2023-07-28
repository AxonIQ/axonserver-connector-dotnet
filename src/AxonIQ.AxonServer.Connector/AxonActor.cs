using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AxonActor<TMessage, TState> : IAxonActor<TMessage>, IAsyncDisposable
{
    private readonly Func<TMessage, TState, CancellationToken, Task<TState>> _receiver;
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Channel<TMessage> _inbox;
    private readonly Task _consumer;
    
    private TState _state;
    private long _disposed;

    public AxonActor(
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
        
        _inbox = Channel.CreateUnbounded<TMessage>(
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
                while (_inbox.Reader.TryRead(out var message))
                {
                    if (message == null) continue;
                    
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Received {Message} when {State}", message.ToString(), _state!.ToString());
                    }

                    _state = await _receiver(message, _state, ct);
                    
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Completed {Message} with {State}", message.ToString(), _state!.ToString());
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
        return _scheduler.ScheduleTaskAsync(() => TellAsync(message, _inboxCancellation.Token), due);
    }

    public ValueTask ScheduleAsync(TMessage message, TimeSpan due, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _scheduler.ScheduleTaskAsync(() => TellAsync(message, ct), due);
    }
    
    public bool TryTell(TMessage message)
    {
        ThrowIfDisposed();
        return _inbox.Writer.TryWrite(message);
    }

    public ValueTask TellAsync(TMessage message)
    {
        ThrowIfDisposed();
        return TellAsync(message, _inboxCancellation.Token);
    }

    public ValueTask TellAsync(TMessage message, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _inbox.Writer.WriteAsync(message, ct);
    }

    public async ValueTask TellAsync(IReadOnlyCollection<TMessage> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();
        
        foreach (var message in messages)
        {
            await TellAsync(message, _inboxCancellation.Token);
        }
    }

    public async ValueTask TellAsync(IReadOnlyCollection<TMessage> messages, CancellationToken ct)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        ThrowIfDisposed();

        foreach (var message in messages)
        {
            await TellAsync(message, ct);
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