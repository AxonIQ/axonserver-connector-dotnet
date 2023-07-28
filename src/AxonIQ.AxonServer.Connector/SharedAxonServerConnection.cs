using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class SharedAxonServerConnection : IAxonServerConnection, IOwnerAxonServerConnection
{
    private readonly AxonServerConnectionFactory _factory;
    private readonly Context _context;
    private readonly CallInvokerProxy _callInvoker;
    private readonly ILogger<SharedAxonServerConnection> _logger;

    private readonly AxonActor<Message, State> _actor;
    private readonly ControlChannel _controlChannel;
    private readonly Lazy<AdminChannel> _adminChannel;
    private readonly Lazy<CommandChannel> _commandChannel;
    private readonly Lazy<QueryChannel> _queryChannel;
    private readonly Lazy<EventChannel> _eventChannel;
    
    private long _disposed;

    public SharedAxonServerConnection(AxonServerConnectionFactory factory, Context context)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _context = context;
        
        _logger = factory.LoggerFactory.CreateLogger<SharedAxonServerConnection>();
        
        _actor = new AxonActor<Message, State>(
            Receive,
            new State.Disconnected(new WaitUntil()),
            factory.Scheduler,
            _logger);
        
        _callInvoker = new CallInvokerProxy(() => _actor.State.CallInvoker);
        
        _controlChannel = new ControlChannel(
            this,
            factory.Scheduler,
            factory.EventProcessorUpdateFrequency,
            factory.LoggerFactory);
        _adminChannel = new Lazy<AdminChannel>(() => new AdminChannel(this));
        _commandChannel = new Lazy<CommandChannel>(() => new CommandChannel(
            this,
            factory.Scheduler,
            factory.CommandPermits,
            new PermitCount(factory.CommandPermits.ToInt64() / 4L),
            factory.ReconnectOptions,
            factory.LoggerFactory));
        _queryChannel = new Lazy<QueryChannel>(() => new QueryChannel(
            factory.ChannelFactory.ClientIdentity,
            _context,
            factory.Scheduler.Clock,
            _callInvoker,
            factory.QueryPermits,
            new PermitCount(factory.QueryPermits.ToInt64() / 4L),
            factory.LoggerFactory));
        _eventChannel = new Lazy<EventChannel>(() => new EventChannel(
            this,
            factory.Scheduler.Clock,
            factory.LoggerFactory));
    }

    public ClientIdentity ClientIdentity => _factory.ChannelFactory.ClientIdentity;

    public Context Context => _context;

    CallInvoker IOwnerAxonServerConnection.CallInvoker => _callInvoker;

#pragma warning disable CS4014
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected disconnected, Message.Connect connect):
                if (connect.Attempt == 0)
                {
                    _factory.ChannelFactory
                        .Create(_context)
                        .TellToAsync(_actor,
                            result => new Message.GrpcChannelEstablished(result),
                            ct);
                    
                    state = new State.Connecting(connect.Attempt, disconnected.WaitUntil);
                }

                break;
            case (State.Connecting, Message.Connect):
                _factory.ChannelFactory
                    .Create(_context)
                    .TellToAsync(_actor,
                        result => new Message.GrpcChannelEstablished(result),
                        ct);
                break;
            case (State.Connecting connecting, Message.GrpcChannelEstablished established):
                switch (established.Result)
                {
                    case TaskResult<GrpcChannel?>.Ok ok:
                        if (ok.Value != null)
                        {
                            var callInvoker = ok.Value
                                .CreateCallInvoker()
                                .Intercept(_factory.Interceptors.ToArray())
                                .Intercept(metadata =>
                                {
                                    _factory.ChannelFactory.Authentication.WriteTo(metadata);
                                    _context.WriteTo(metadata);
                                    return metadata;
                                });

                            state = new State.Connected(ok.Value, callInvoker, connecting.WaitUntil);
                            
                            await _actor.TellAsync(new Message[]
                            {
                                new Message.ConnectControlChannel(),
                                new Message.OnConnected(), 
                                new Message.CheckReadiness()
                            }, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Could not create channel for context '{Context}'",
                                _context);

                            await _actor.ScheduleAsync(new Message.Connect(connecting.Attempt + 1), TimeSpan.FromMilliseconds(500), ct);
                        }
                        break;
                    case TaskResult<GrpcChannel?>.Error error:
                        _logger.LogError(
                            error.Exception,
                            "Could not create channel for context '{Context}'",
                            _context);

                        await _actor.ScheduleAsync(new Message.Connect(connecting.Attempt + 1), TimeSpan.FromMilliseconds(500), ct);
                        
                        break;
                }

                break;
            case (State.Connected connected, Message.Reconnect reconnect):
                try
                {
                    await connected.Channel.ShutdownAsync();
                    connected.Channel.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Failed to shut down channel");
                }
                
                _factory
                    .ChannelFactory
                    .Create(_context)
                    .TellToAsync(_actor,
                        result => new Message.GrpcChannelEstablished(result),
                        ct);

                state = new State.Reconnecting(reconnect.Attempt, connected.WaitUntil); 
                
                break;
            case (State.Reconnecting, Message.Reconnect):
                _factory
                    .ChannelFactory
                    .Create(_context)
                    .TellToAsync(_actor,
                        result => new Message.GrpcChannelEstablished(result),
                        ct);

                break;
            case (State.Reconnecting reconnecting, Message.GrpcChannelEstablished established):
                switch (established.Result)
                {
                    case TaskResult<GrpcChannel?>.Ok ok:
                        if (ok.Value != null)
                        {
                            var callInvoker = ok.Value
                                .CreateCallInvoker()
                                .Intercept(_factory.Interceptors.ToArray())
                                .Intercept(metadata =>
                                {
                                    _factory.ChannelFactory.Authentication.WriteTo(metadata);
                                    _context.WriteTo(metadata);
                                    return metadata;
                                });
                            
                            state = new State.Connected(ok.Value, callInvoker, reconnecting.WaitUntil);
                            
                            await _actor.TellAsync(new Message[] { 
                                new Message.ReconnectChannels(),
                                new Message.OnConnected(),
                                new Message.CheckReadiness()
                            }, ct);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Could not create channel for context '{Context}'",
                                _context);

                            await _actor.ScheduleAsync(new Message.Reconnect(reconnecting.Attempt + 1), TimeSpan.FromMilliseconds(500), ct);
                        }
                        break;
                    case TaskResult<GrpcChannel?>.Error error:
                        _logger.LogError(
                            error.Exception,
                            "Could not create channel for context '{Context}'",
                            _context);

                        await _actor.ScheduleAsync(new Message.Reconnect(reconnecting.Attempt + 1), TimeSpan.FromMilliseconds(500), ct);
                        
                        break;
                }

                break;
            case (State.Connected, Message.ConnectControlChannel):
                await _controlChannel.Connect().ConfigureAwait(false);

                break;
            case (State.Connected, Message.ReconnectChannels):
                await _controlChannel.Reconnect().ConfigureAwait(false);
                
                if (_commandChannel.IsValueCreated)
                {
                    await _commandChannel.Value.Reconnect().ConfigureAwait(false);
                }

                if (_eventChannel.IsValueCreated)
                {
                    _eventChannel.Value.Reconnect();
                }
                
                if (_queryChannel.IsValueCreated)
                {
                    await _queryChannel.Value.Reconnect().ConfigureAwait(false);
                }

                break;
            case (State.Connected, Message.WaitUntilConnected waitUntilConnected):
                waitUntilConnected.Completion.SetResult();
                break;
            case (_, Message.WaitUntilConnected waitUntilConnected):
                state.WaitUntil.Connected(waitUntilConnected.Completion);
                break;
            case (_, Message.WaitUntilReady waitUntilReady):
                if (!IsReady)
                {
                    state.WaitUntil.Ready(waitUntilReady.Completion);
                }
                else
                {
                    waitUntilReady.Completion.SetResult();
                }

                break;
            case (_, Message.CheckReadiness):
                if (IsReady) state.WaitUntil.OnReady();
                break;
            
            case (State.Connected, Message.OnConnected):
                state.WaitUntil.OnConnected();
                break;
            default:
                _logger.LogWarning("Skipped {Message} in {State}", message, state);
                break;
        }

        return state;
    }
#pragma warning restore CS4014

    async ValueTask IOwnerAxonServerConnection.ReconnectAsync()
    {
        await _actor.TellAsync(
            new Message.Reconnect(0)
        ).ConfigureAwait(false);
    }

    async ValueTask IOwnerAxonServerConnection.CheckReadinessAsync()
    {
        await _actor.TellAsync(
            new Message.CheckReadiness()
        ).ConfigureAwait(false);
    }

    internal async ValueTask ConnectAsync()
    {
        await _actor.TellAsync(
            new Message.Connect(0)
        ).ConfigureAwait(false);
    }

    public async Task WaitUntilConnectedAsync()
    {
        ThrowIfDisposed();
        if (!IsConnected)
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor.TellAsync(new Message.WaitUntilConnected(source));
            await source.Task.WaitAsync(_actor.CancellationToken);
        }
    }
    
    public async Task WaitUntilReadyAsync()
    {
        ThrowIfDisposed();
        if (!IsReady)
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor.TellAsync(new Message.WaitUntilReady(source));
            await source.Task.WaitAsync(_actor.CancellationToken);
        }
    }

    public bool IsConnected => Interlocked.Read(ref _disposed) == Disposed.No && _actor.State is State.Connected;

    public bool IsClosed => Interlocked.Read(ref _disposed) == Disposed.Yes;

    public bool IsReady =>
        Interlocked.Read(ref _disposed) == Disposed.No 
        && IsConnected
        && _controlChannel.IsConnected
        // _adminChannel does not have this notion
        && (!_commandChannel.IsValueCreated || _commandChannel.Value.IsConnected)
        // _eventChannel does not have this notion
        && (!_queryChannel.IsValueCreated || _queryChannel.Value.IsConnected);

    private record Message
    {
        public record Connect(ulong Attempt) : Message;
        public record ConnectControlChannel : Message;
        
        public record Reconnect(ulong Attempt) : Message;
        public record ReconnectChannels : Message;

        public record GrpcChannelEstablished(TaskResult<GrpcChannel?> Result) : Message;

        public record WaitUntilConnected(TaskCompletionSource Completion) : Message;
        public record WaitUntilReady(TaskCompletionSource Completion) : Message;

        public record OnConnected : Message;
        public record CheckReadiness : Message;
    }
    
    private class WaitUntil
    {
        private readonly List<TaskCompletionSource> _connected = new();
        private readonly List<TaskCompletionSource> _ready = new();

        public void Connected(TaskCompletionSource source)
        {
            _connected.Add(source);
        }
        
        public void Ready(TaskCompletionSource source)
        {
            _ready.Add(source);
        }
        
        public void OnConnected()
        {
            _connected.ForEach(completion => completion.SetResult());
            _connected.Clear();
        }
        
        public void OnReady()
        {
            _ready.ForEach(completion => completion.SetResult());
            _ready.Clear();
        }
    }

    private record State(CallInvoker? CallInvoker, WaitUntil WaitUntil)
    {
        public record Disconnected(WaitUntil WaitUntil) : State(null, WaitUntil);

        public record Connecting(ulong Attempt, WaitUntil WaitUntil) : State(null, WaitUntil);
        public record Reconnecting(ulong Attempt, WaitUntil WaitUntil) : State(null, WaitUntil);

        public record Connected(GrpcChannel Channel, CallInvoker CallInvoker, WaitUntil WaitUntil) : State(CallInvoker, WaitUntil);
    }

    public IControlChannel ControlChannel
    {
        get { ThrowIfDisposed(); return _controlChannel; }
    }

    public IAdminChannel AdminChannel
    {
        get { ThrowIfDisposed(); return _adminChannel.Value; }
    }

    public ICommandChannel CommandChannel
    {
        get { ThrowIfDisposed(); return _commandChannel.Value; }
    }

    public IQueryChannel QueryChannel
    {
        get { ThrowIfDisposed(); return _queryChannel.Value; }
    }

    public IEventChannel EventChannel
    {
        get { ThrowIfDisposed(); return _eventChannel.Value; }
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes) 
            throw new ObjectDisposedException(nameof(SharedAxonServerConnection));
    }
    
    public Task CloseAsync() => DisposeAsync().AsTask();
    
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
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
            if (_actor.State is State.Connected connected)
            {
                try
                {
                    await connected.Channel.ShutdownAsync().ConfigureAwait(false);
                    connected.Channel.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Failed to shut down and dispose gRPC channel");
                }
            }

            await _factory.TryRemoveConnectionAsync(_context, this);
        }
    }
}