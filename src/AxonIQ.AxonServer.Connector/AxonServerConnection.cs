using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
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
    private readonly ILogger<AxonServerConnection> _logger;

    private readonly AxonActor<Protocol, State> _actor;
    private readonly ControlChannel _controlChannel;
    private readonly Lazy<AdminChannel> _adminChannel;
    private readonly Lazy<CommandChannel> _commandChannel;
    private readonly Lazy<QueryChannel> _queryChannel;
    private readonly Lazy<EventChannel> _eventChannel;
    private readonly EventHandler _onConnectedHandler;

    public AxonServerConnection(Context context,
        AxonServerGrpcChannelFactory channelFactory,
        IReadOnlyList<Interceptor> interceptors,
        IScheduler scheduler,
        PermitCount commandPermits,
        PermitCount queryPermits,
        TimeSpan eventProcessorUpdateFrequency,
        BackoffPolicyOptions connectBackoffPolicyOptions,
        ILoggerFactory loggerFactory)
    {
        if (channelFactory == null)
            throw new ArgumentNullException(nameof(channelFactory));
        if (interceptors == null)
            throw new ArgumentNullException(nameof(interceptors));
        if (scheduler == null)
            throw new ArgumentNullException(nameof(scheduler));
        if (connectBackoffPolicyOptions == null)
            throw new ArgumentNullException(nameof(connectBackoffPolicyOptions));
        if (loggerFactory == null)
            throw new ArgumentNullException(nameof(loggerFactory));

        _context = context;
        _channelFactory = channelFactory;
        _interceptors = interceptors;
        _logger = loggerFactory.CreateLogger<AxonServerConnection>();
        
        _actor = new AxonActor<Protocol, State>(
            Receive,
            new ConnectionOwnedState(this, new State.Disconnected()),
            scheduler,
            _logger);
        var callInvokerProxy = new CallInvokerProxy(() => _actor.State.CallInvoker);
        _controlChannel = new ControlChannel(
            channelFactory.ClientIdentity,
            _context,
            callInvokerProxy,
            Reconnect,
            scheduler,
            eventProcessorUpdateFrequency,
            loggerFactory);
        _adminChannel = new Lazy<AdminChannel>(() => new AdminChannel(
            channelFactory.ClientIdentity,
            callInvokerProxy));
        _commandChannel = new Lazy<CommandChannel>(() => new CommandChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler,
            callInvokerProxy,
            commandPermits,
            new PermitCount(commandPermits.ToInt64() / 4L),
            connectBackoffPolicyOptions,
            loggerFactory));
        _queryChannel = new Lazy<QueryChannel>(() => new QueryChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler.Clock,
            callInvokerProxy,
            queryPermits,
            new PermitCount(queryPermits.ToInt64() / 4L),
            loggerFactory));
        _eventChannel = new Lazy<EventChannel>(() => new EventChannel(
            channelFactory.ClientIdentity,
            _context,
            scheduler.Clock,
            callInvokerProxy,
            loggerFactory));
        _onConnectedHandler = (_, _) => { OnReady(); };
        _controlChannel.Connected += _onConnectedHandler;
    }

    private async Task<State> Receive(Protocol message, State state, CancellationToken ct)
    {
        switch (message)
        {
            case Protocol.Connect:
                switch (state)
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
                            state = new State.Connected(channel, callInvoker);
                            //State needs to be set for the control channel to pick up
                            //the right call invoker - therefor we use a message instead
                            await _actor.TellAsync(new Protocol.OnConnected(), ct);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Could not create channel for context '{Context}'",
                                _context);

                            await _actor.ScheduleAsync(new Protocol.Connect(), TimeSpan.FromMilliseconds(500), ct);
                        }

                        break;
                    case State.Connected:
                        _logger.LogInformation(
                            "AxonServerConnection for context '{Context}' is already connected",
                            _context.ToString());
                        break;
                }

                break;
            case Protocol.OnConnected:
                if (state is State.Connected)
                {
                    await _controlChannel.Connect().ConfigureAwait(false);
                }

                break;
            case Protocol.Reconnect:
                switch (state)
                {
                    case State.Connected connected:
                        await _controlChannel.Reconnect().ConfigureAwait(false);

                        _logger.LogInformation(
                            "Reconnect for context {Context} requested. Closing current connection",
                            _context);
                        await connected.Channel.ShutdownAsync().ConfigureAwait(false);
                        connected.Channel.Dispose();

                        state = new State.Disconnected();

                        await _actor.TellAsync(new Protocol.Connect(), ct).ConfigureAwait(false);

                        break;
                }

                break;
        }

        return state;
    }

    public async Task Connect()
    {
        await _actor.TellAsync(
            new Protocol.Connect()
        ).ConfigureAwait(false);
    }

    private async ValueTask Reconnect()
    {
        await _actor.TellAsync(
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

    public bool IsConnected => _actor.State is State.Connected;

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
        public record OnConnected : Protocol;

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
        _controlChannel.Connected -= _onConnectedHandler;
        await _controlChannel.DisposeAsync().ConfigureAwait(false);
        if (_commandChannel.IsValueCreated)
        {
            await _commandChannel.Value.DisposeAsync().ConfigureAwait(false);
        }

        if (_queryChannel.IsValueCreated)
        {
            await _queryChannel.Value.DisposeAsync().ConfigureAwait(false);
        }

        await _actor.DisposeAsync().ConfigureAwait(false);
    }

    private class ConnectionOwnedState : IAxonActorStateOwner<State>
    {
        private readonly AxonServerConnection _connection;
        private State _state;

        public ConnectionOwnedState(AxonServerConnection connection, State initialState)
        {
            _connection = connection;
            _state = initialState;
        }

        public State State
        {
            get => _state;
            set
            {
                var connected = _state is not State.Connected && value is State.Connected;
                var disconnected = _state is not State.Disconnected && value is State.Disconnected;
                _state = value;
                if (connected) _connection.OnConnected();
                if (disconnected) _connection.OnDisconnected();
            }
        }
    }
}