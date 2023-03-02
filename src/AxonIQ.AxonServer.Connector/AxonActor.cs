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

    public AxonActor(Func<TProtocol, TState, CancellationToken, Task<TState>> receiver, TState initialState, IScheduler scheduler, ILogger logger)
        : this(receiver, new SelfOwnedState(initialState), scheduler, logger)
    {
    }
    
    public AxonActor(Func<TProtocol, TState, CancellationToken, Task<TState>> receiver, IAxonActorStateOwner<TState> owner, IScheduler scheduler, ILogger logger)
    {
        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _inboxCancellationTokenSource = new CancellationTokenSource();
        
        _inbox = Channel.CreateUnbounded<TProtocol>(new UnboundedChannelOptions
            { SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = false });

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
                        _logger.LogDebug("Began {Message} when {State}", message.ToString(), _owner.State!.ToString());
                    }

                    _owner.State = await _receiver(message, _owner.State, ct);

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Completed {Message} with {State}", message.ToString(), _owner.State!.ToString());
                    }
                }
            }
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Channel protocol loop is exciting because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Channel protocol loop is exciting because of an unexpected exception");
        }
    }

    public Func<DateTimeOffset> Clock => _scheduler.Clock;

    public TState State => _owner.State;

    public ValueTask ScheduleAsync(TProtocol message, TimeSpan due, CancellationToken ct) =>
        _scheduler.ScheduleTask(() => _inbox.Writer.WriteAsync(message, ct), due);

    public ValueTask TellAsync(TProtocol message) => TellAsync(message, _inboxCancellationTokenSource.Token);
    public ValueTask TellAsync(TProtocol message, CancellationToken ct) => _inbox.Writer.WriteAsync(message, ct);

    //TODO: Need to get rid of events and then this can follow.
    public void TellSync(TProtocol message)
    {
        if (!_inbox.Writer.TryWrite(message))
        {
            _logger.LogDebug(
                "Could not tell the connection to reconnect because the inbox refused to accept the message");
        }
    }

    public bool IsCancellationRequested => _inboxCancellationTokenSource.IsCancellationRequested;

    public async ValueTask DisposeAsync()
    {
        _inboxCancellationTokenSource.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion.ConfigureAwait(false);
        await _consumer.ConfigureAwait(false);
        _inboxCancellationTokenSource.Dispose();
        _consumer.Dispose();
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