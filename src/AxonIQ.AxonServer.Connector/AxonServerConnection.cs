using System.Threading.Channels;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnection : IAxonServerConnection
{
    private readonly AxonServerGrpcChannelFactory _channelFactory;
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
    private readonly EventHandler _onConnectedHandler;
    private readonly EventHandler _onHeartbeatMissedHandler;

    public AxonServerConnection(
        Context context,
        AxonServerGrpcChannelFactory channelFactory,
        IScheduler scheduler,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _channelFactory = channelFactory ?? throw new ArgumentNullException(nameof(channelFactory));
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
            _loggerFactory);
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
            if (_state is not State.Connected && value is State.Connected)
            {
                OnConnected();
            }

            if (_state is not State.Disconnected && value is State.Disconnected)
            {
                OnDisconnected();
            }

            _state = value;
        }
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        while (await _inbox.Reader.WaitToReadAsync(ct))
        {
            while (_inbox.Reader.TryRead(out var message))
            {
                switch (message)
                {
                    case Protocol.Connect:
                        switch (CurrentState)
                        {
                            case State.Disconnected:
                                var channel = await _channelFactory.Create(_context);
                                if (channel != null)
                                {
                                    var callInvoker = channel.CreateCallInvoker().Intercept(metadata =>
                                    {
                                        _channelFactory.Authentication.WriteTo(metadata);
                                        _context.WriteTo(metadata);
                                        return metadata;
                                    });
                                    //State needs to be set for the control channel to pick up
                                    //the right call invoker
                                    CurrentState = new State.Connected(channel, callInvoker);
                                    await _controlChannel.Connect();
                                }

                                break;
                            case State.Connected:
                                _logger.LogInformation("AxonServerConnection for context '{Context}' is already connected", _context.ToString());
                                break;
                        }

                        break;
                    case Protocol.Reconnect:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                _logger.LogInformation("Reconnect for context {Context} requested. Closing current connection", _context);
                                await connected.Channel.ShutdownAsync();
                                connected.Channel.Dispose();
                                
                                CurrentState = new State.Disconnected();

                                await _controlChannel.Reconnect();

                                await _scheduler.ScheduleTask(
                                    () => _inbox.Writer.WriteAsync(new Protocol.Connect(), ct), _scheduler.Clock());
                                break;
                        }

                        break;
                }
            }
        }
    }

    public async Task Connect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Connect()
        );
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

    public bool IsReady => IsConnected && _controlChannel.IsConnected;

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

    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion;
        await _protocol;
        _controlChannel.Connected -= _onConnectedHandler;
        _controlChannel.HeartbeatMissed -= _onHeartbeatMissedHandler;
        await _controlChannel.DisposeAsync();
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}