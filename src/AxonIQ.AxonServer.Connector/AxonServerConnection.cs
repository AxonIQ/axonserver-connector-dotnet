using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnection : IAxonServerConnection
{
    private readonly AxonServerGrpcChannelFactory _channelFactory;
    private readonly IReadOnlyList<Interceptor> _interceptors;
    private readonly Context _context;
    private readonly IScheduler _scheduler;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AxonServerConnection> _logger;
    
    private State _state;
    private readonly CallInvokerProxy _callInvokerProxy;
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    private readonly ControlChannel _controlChannel;
    private readonly Lazy<AdminChannel> _adminChannel;
    private readonly Lazy<CommandChannel> _commandChannel;
    private readonly Lazy<QueryChannel> _queryChannel;
    private readonly Lazy<EventChannel> _eventChannel;
    private readonly EventHandler _onConnectedHandler;
    private readonly EventHandler _onHeartbeatMissedHandler;

    public AxonServerConnection(Context context,
        AxonServerGrpcChannelFactory channelFactory,
        IReadOnlyList<Interceptor> interceptors,
        IScheduler scheduler,
        PermitCount commandPermits,
        PermitCount queryPermits,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
        _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<AxonServerConnection>();

        _state = new State.Disconnected();
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        _callInvokerProxy = new CallInvokerProxy(() => CurrentState.CallInvoker);
        _controlChannel = new ControlChannel(
            channelFactory.ClientIdentity,
            _context,
            _callInvokerProxy,
            Reconnect,
            _scheduler.Clock,
            _loggerFactory);
        _adminChannel = new Lazy<AdminChannel>(() => new AdminChannel(
            channelFactory.ClientIdentity,
            _callInvokerProxy));
        _commandChannel = new Lazy<CommandChannel>(() => new CommandChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler.Clock,
            _callInvokerProxy,
            commandPermits,
            new PermitCount(commandPermits.ToInt64() / 4L),
            _loggerFactory));
        _queryChannel = new Lazy<QueryChannel>(() => new QueryChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler.Clock,
            _callInvokerProxy,
            queryPermits,
            new PermitCount(queryPermits.ToInt64() / 4L),
            _loggerFactory));
        _eventChannel = new Lazy<EventChannel>(() => new EventChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler.Clock,
            _callInvokerProxy,
            _loggerFactory));
        _onConnectedHandler = (_, _) =>
        {
            OnReady();
        };
        _controlChannel.Connected += _onConnectedHandler;
        _onHeartbeatMissedHandler = (_, _) =>
        {
            if (!_inbox.Writer.TryWrite(new Protocol.Reconnect()))
            {
                _logger.LogDebug(
                    "Could not tell the connection to reconnect because the inbox refused to accept the message");
            }
        };
        _controlChannel.HeartbeatMissed += _onHeartbeatMissedHandler;
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }

    private State CurrentState
    {
        get => _state;
        set
        {
            var connected = _state is not State.Connected && value is State.Connected;
            var disconnected = _state is not State.Disconnected && value is State.Disconnected;
            _state = value;
            if (connected) OnConnected();
            if (disconnected) OnDisconnected();
        }
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        try
        {
            while (await _inbox.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_inbox.Reader.TryRead(out var message))
                {
                    _logger.LogDebug("Began {Message} when {State}", message.ToString(), CurrentState.ToString());
                    switch (message)
                    {
                        case Protocol.Connect:
                            switch (CurrentState)
                            {
                                case State.Disconnected:
                                    var channel = await _channelFactory.Create(_context).ConfigureAwait(false);
                                    if (channel != null)
                                    {
                                        var callInvoker = channel
                                            .CreateCallInvoker()
                                            .Intercept(_interceptors.ToArray())
                                            .Intercept(metadata =>
                                            {
                                                _channelFactory.Authentication.WriteTo(metadata);
                                                _context.WriteTo(metadata);
                                                return metadata;
                                            });
                                        //State needs to be set for the control channel to pick up
                                        //the right call invoker
                                        CurrentState = new State.Connected(channel, callInvoker);
                                        await _controlChannel.Connect().ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Could not create channel for context '{Context}'",
                                            _context);
                                    }

                                    break;
                                case State.Connected:
                                    _logger.LogInformation(
                                        "AxonServerConnection for context '{Context}' is already connected",
                                        _context.ToString());
                                    break;
                            }

                            break;
                        case Protocol.Reconnect:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    await _controlChannel.Reconnect().ConfigureAwait(false);

                                    _logger.LogInformation(
                                        "Reconnect for context {Context} requested. Closing current connection",
                                        _context);
                                    await connected.Channel.ShutdownAsync().ConfigureAwait(false);
                                    connected.Channel.Dispose();

                                    CurrentState = new State.Disconnected();

                                    await _scheduler.ScheduleTask(
                                        () => _inbox.Writer.WriteAsync(new Protocol.Connect(), ct), _scheduler.Clock()
                                    ).ConfigureAwait(false);
                                    break;
                            }

                            break;
                    }
                    _logger.LogDebug("Completed {Message} with {State}", message.ToString(), CurrentState.ToString());
                }
            }
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Axon Server connection message loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Axon Server connection message loop is exciting because an operation was cancelled");
        }
    }

    public async Task Connect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Connect()
        ).ConfigureAwait(false);
    }

    private async ValueTask Reconnect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Reconnect()
        ).ConfigureAwait(false);
        
        if (_commandChannel.IsValueCreated)
        {
            await _commandChannel.Value.Reconnect().ConfigureAwait(false);
        }
        
        if (_queryChannel.IsValueCreated)
        {
            await _queryChannel.Value.Reconnect().ConfigureAwait(false);
        }
        
        if (_eventChannel.IsValueCreated)
        {
            await _eventChannel.Value.Reconnect().ConfigureAwait(false);
        }
    }

    public Task WaitUntilConnected()
    {
        if (IsConnected)
        {
            return Task.CompletedTask;
        }
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            source.TrySetResult();
            Connected -= handler;
        }; 
        Connected += handler;
        if (IsConnected)
        {
            Connected -= handler;
            return Task.CompletedTask;
        }
        return source.Task;
    }

    public event EventHandler? Connected;
    
    protected virtual void OnConnected()
    {
        Connected?.Invoke(this, EventArgs.Empty);
    }
    
    public event EventHandler? Disconnected;
    
    protected virtual void OnDisconnected()
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
    
    public bool IsConnected => CurrentState is State.Connected;
    
    public event EventHandler? Ready;
    
    protected virtual void OnReady()
    {
        Ready?.Invoke(this, EventArgs.Empty);
    }

    public bool IsReady => 
        IsConnected 
        && _controlChannel.IsConnected
        // _adminChannel does not have this notion
        && (!_commandChannel.IsValueCreated || _commandChannel.Value.IsConnected)
        // _eventChannel does not have this notion
        && (!_queryChannel.IsValueCreated || _queryChannel.Value.IsConnected);
    
    public Task WaitUntilReady()
    {
        if (IsReady)
        {
            return Task.CompletedTask;
        }
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            source.TrySetResult();
            Ready -= handler;
        }; 
        Ready += handler;
        if (IsReady)
        {
            Ready -= handler;
            return Task.CompletedTask;
        }
        return source.Task;
    }

    private record Protocol
    {
        public record Connect : Protocol;

        public record Reconnect : Protocol;
    }

    private record State(CallInvoker? CallInvoker)
    {
        public record Disconnected() : State((CallInvoker?)null);

        public record Connected(GrpcChannel Channel, CallInvoker CallInvoker) : State(CallInvoker);
    }
    
    public IControlChannel ControlChannel => _controlChannel;
    
    public IAdminChannel AdminChannel => _adminChannel.Value;

    public ICommandChannel CommandChannel => _commandChannel.Value;

    public IQueryChannel QueryChannel => _queryChannel.Value;

    public IEventChannel EventChannel => _eventChannel.Value;

    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion.ConfigureAwait(false);
        await _protocol.ConfigureAwait(false);
        _controlChannel.Connected -= _onConnectedHandler;
        _controlChannel.HeartbeatMissed -= _onHeartbeatMissedHandler;
        await _controlChannel.DisposeAsync().ConfigureAwait(false);
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}