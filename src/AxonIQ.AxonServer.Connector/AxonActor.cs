using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AxonActor<TProtocol, TState> : IAsyncDisposable
{
    private readonly Func<TProtocol, TState, CancellationToken, Task<TState>> _receiver;
    private readonly IAxonActorStateOwner<TState> _owner;
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _inboxCancellationTokenSource;
    private readonly Channel<TProtocol> _inbox;
    private readonly Task _consumer;

    public AxonActor(Func<TProtocol, TState, CancellationToken, Task<TState>> receiver, TState initialState,
        IScheduler scheduler, ILogger logger)
        : this(receiver, new SelfOwnedState(initialState), scheduler, logger)
    {
    }
    
    public AxonActor(Func<TProtocol, TState, CancellationToken, Task<TState>> receiver,
        IAxonActorStateOwner<TState> owner, IScheduler scheduler, ILogger logger)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _inboxCancellationTokenSource = new CancellationTokenSource();
        
        _inbox = Channel.CreateUnbounded<TProtocol>(
            new UnboundedChannelOptions
            {
                SingleWriter = false, 
                SingleReader = true, 
                AllowSynchronousContinuations = false // TODO: Figure out if this needs to be false or true
            });

        _consumer = ConsumeInbox(_inboxCancellationTokenSource.Token);
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
                        _logger.LogDebug("Received {Message} when {State}", message.ToString(), _owner.State!.ToString());
                    }

                    _owner.State = await _receiver(message, _owner.State, ct);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Completed {Message} with {State}", message.ToString(), _owner.State!.ToString());
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

    public TState State => _owner.State;

    public ValueTask ScheduleAsync(TProtocol message, TimeSpan due) =>
        _scheduler.ScheduleTask(() => TellAsync(message), due);
    
    public ValueTask ScheduleAsync(TProtocol message, TimeSpan due, CancellationToken ct) =>
        _scheduler.ScheduleTask(() => TellAsync(message, ct), due);

    public ValueTask TellAsync(TProtocol message) => TellAsync(message, _inboxCancellationTokenSource.Token);
    public ValueTask TellAsync(TProtocol message, CancellationToken ct) => _inbox.Writer.WriteAsync(message, ct);

    public bool IsCancellationRequested => _inboxCancellationTokenSource.IsCancellationRequested;

    public async ValueTask DisposeAsync()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposing actor");
        }
        _inboxCancellationTokenSource.Cancel();
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Cancelled actor's token source");
        }
        _inbox.Writer.Complete();
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Completed actor's inbox writer");
        }
        await _consumer.ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Awaited actor's inbox consumer's completion");
        }
        _inboxCancellationTokenSource.Dispose();
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposed actor's cancellation token source");
        }
        _consumer.Dispose();
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposed actor");
        }
    }
    
    private class SelfOwnedState: IAxonActorStateOwner<TState>
    {
        public SelfOwnedState(TState initialState)
        {
            State = initialState;
        }
        
        public TState State { get; set; }
    }
}