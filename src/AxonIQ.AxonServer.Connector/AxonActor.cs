using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AxonActor<TMessage, TState> : IAxonActor<TMessage>, IAsyncDisposable
{
    private static class Ignore
    {
        public static readonly AxonActorStateChanged<TState> StateChanged = (_,_) => ValueTask.CompletedTask;
    }
    
    private readonly Func<TMessage, TState, CancellationToken, Task<TState>> _receiver;
    private readonly AxonActorStateChanged<TState> _onStateChanged;
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Channel<TMessage> _inbox;
    private readonly Task _consumer;
    
    private TState _state;

    public AxonActor(
        Func<TMessage, TState, CancellationToken, Task<TState>> receiver, 
        TState initialState,
        IScheduler scheduler, 
        ILogger logger)
        : this(receiver, Ignore.StateChanged,  initialState, scheduler, logger)
    {
    }
    
    public AxonActor(
        Func<TMessage, TState, CancellationToken, Task<TState>> receiver,
        AxonActorStateChanged<TState> onStateChanged,
        TState initialState,
        IScheduler scheduler,
        ILogger logger)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _onStateChanged = onStateChanged ?? throw new ArgumentNullException(nameof(onStateChanged));
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
        var notifyStateChanged = !ReferenceEquals(_onStateChanged, Ignore.StateChanged);
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

                    var before = _state;
                    var after = await _receiver(message, before, ct);
                    _state = after;
                    
                    if (notifyStateChanged && !ReferenceEquals(before, after))
                    {
                        await _onStateChanged(before, after);
                    }
                    
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
                    $"Actor protocol loop is exciting because an operation was cancelled");    
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Actor protocol loop is exciting because of an unexpected exception");
        }
    }

    public Func<DateTimeOffset> Clock => _scheduler.Clock;

    public TState State => _state;

    public ValueTask ScheduleAsync(TMessage message, TimeSpan due) =>
        _scheduler.ScheduleTask(() => TellAsync(message), due);
    
    public ValueTask ScheduleAsync(TMessage message, TimeSpan due, CancellationToken ct) =>
        _scheduler.ScheduleTask(() => TellAsync(message, ct), due);

    public ValueTask TellAsync(TMessage message) => TellAsync(message, _inboxCancellation.Token);
    public ValueTask TellAsync(TMessage message, CancellationToken ct) => _inbox.Writer.WriteAsync(message, ct);
    
    public async ValueTask TellAsync(IReadOnlyCollection<TMessage> messages)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        foreach (var message in messages)
        {
            await TellAsync(message);
        }
    }

    public async ValueTask TellAsync(IReadOnlyCollection<TMessage> messages, CancellationToken ct)
    {
        if (messages == null) throw new ArgumentNullException(nameof(messages));
        foreach (var message in messages)
        {
            await TellAsync(message, ct);
        }
    }

    public bool IsCancellationRequested => _inboxCancellation.IsCancellationRequested;

    public async ValueTask DisposeAsync()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposing actor");
        }
        
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _consumer.ConfigureAwait(false);
        _inboxCancellation.Dispose();
        _consumer.Dispose();
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposed actor");
        }
    }
}