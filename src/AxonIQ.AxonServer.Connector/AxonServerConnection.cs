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

    private readonly AxonActor<Message, State> _actor;
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

        _actor = new AxonActor<Message, State>(
            Receive,
            (before, after) =>
            {
                if (before is not State.Connected && after is State.Connected)
                {
                    
                    OnConnected();
                }

                if (before is not State.Disconnected && after is State.Disconnected)
                {
                    OnDisconnected();
                }
                
                return ValueTask.CompletedTask;
            }, 
            new State.Disconnected(),
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

#pragma warning disable CS4014
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected, Message.Connect connect):
                if (connect.Attempt == 0)
                {
                    _channelFactory
                        .Create(_context)
                        .TellToAsync(_actor,
                            result => new Message.GrpcChannelEstablished(result),
                            ct);
                    
                    state = new State.Connecting(connect.Attempt);
                }

                break;
            case (State.Connecting connecting, Message.GrpcChannelEstablished established):
                switch (established.Result)
                {
                    case TaskResult<GrpcChannel?>.Ok ok:
                        if (ok.Value != null)
                        {
                            var callInvoker = ok.Value
                                .CreateCallInvoker()
                                .Intercept(_interceptors.ToArray())
                                .Intercept(metadata =>
                                {
                                    _channelFactory.Authentication.WriteTo(metadata);
                                    _context.WriteTo(metadata);
                                    return metadata;
                                });
                            state = new State.Connected(ok.Value, callInvoker);
                            await _actor.TellAsync(new Message.ConnectControlChannel(), ct);
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
            case (State.Connected, Message.ConnectControlChannel):
                await _controlChannel.Connect().ConfigureAwait(false);

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
                
                _channelFactory
                    .Create(_context)
                    .TellToAsync(_actor,
                        result => new Message.GrpcChannelEstablished(result),
                        ct);

                state = new State.Reconnecting(reconnect.Attempt); 
                
                break;
            case (State.Reconnecting reconnecting, Message.GrpcChannelEstablished established):
                switch (established.Result)
                {
                    case TaskResult<GrpcChannel?>.Ok ok:
                        if (ok.Value != null)
                        {
                            var callInvoker = ok.Value
                                .CreateCallInvoker()
                                .Intercept(_interceptors.ToArray())
                                .Intercept(metadata =>
                                {
                                    _channelFactory.Authentication.WriteTo(metadata);
                                    _context.WriteTo(metadata);
                                    return metadata;
                                });
                            state = new State.Connected(ok.Value, callInvoker);
                            await _actor.TellAsync(new Message.ReconnectChannels(), ct);
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
            case (State.Connected, Message.ReconnectChannels):
                await _controlChannel.Reconnect().ConfigureAwait(false);
                
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

                break;
            case (State.Connected connected, Message.Disconnect):
                try
                {
                    await connected.Channel.ShutdownAsync();
                    connected.Channel.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Failed to shut down channel");
                }
                
                state = new State.Disconnected();
                
                break;
            case (State.Connecting, Message.Disconnect):
            case (State.Reconnecting, Message.Disconnect):
                
                state = new State.Disconnected();
                
                break;
        }

        return state;
    }
#pragma warning restore CS4014

    public async Task Connect()
    {
        await _actor.TellAsync(
            new Message.Connect(0)
        ).ConfigureAwait(false);
    }
    
    public async Task Disconnect()
    {
        await _actor.TellAsync(
            new Message.Disconnect()
        ).ConfigureAwait(false);
    }

    private async ValueTask Reconnect()
    {
        await _actor.TellAsync(
            new Message.Reconnect(0)
        ).ConfigureAwait(false);
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

    private record Message
    {
        public record Connect(ulong Attempt) : Message;
        public record ConnectControlChannel : Message;
        
        public record Disconnect : Message;

        public record Reconnect(ulong Attempt) : Message;
        public record ReconnectChannels : Message;

        public record GrpcChannelEstablished(TaskResult<GrpcChannel?> Result) : Message;
    }

    private record State(CallInvoker? CallInvoker)
    {
        public record Disconnected() : State((CallInvoker?)null);
        public record Connecting(ulong Attempt) : State((CallInvoker?)null);
        public record Reconnecting(ulong Attempt) : State((CallInvoker?)null);

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
}