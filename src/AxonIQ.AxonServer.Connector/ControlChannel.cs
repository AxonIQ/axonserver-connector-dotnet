using System.Threading.Channels;
using AxonIQ.AxonServer.Grpc.Control;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class ControlChannel : IControlChannel, IAsyncDisposable
{
    private State _state;
    
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    
    private readonly Context _context;
    private readonly ILogger<ControlChannel> _logger;
    private readonly EventHandler _onHeartbeatMissedHandler;

    public ControlChannel(
        ClientIdentity clientIdentity, 
        Context context, 
        CallInvoker callInvoker,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        ClientIdentity = clientIdentity;
        CallInvoker = callInvoker;
        Service = new PlatformService.PlatformServiceClient(callInvoker);
        
        _context = context;
        _logger = loggerFactory.CreateLogger<ControlChannel>();
        _state = new State.Disconnected();
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        
        InstructionStreamProxy =
            new AsyncDuplexStreamingCallProxy<PlatformInboundInstruction, PlatformOutboundInstruction>(
                () => _state.InstructionStream
            );
        HeartbeatChannel = new HeartbeatChannel(
            instruction => _inbox.Writer.WriteAsync(new Protocol.SendInstruction(instruction)),
            loggerFactory.CreateLogger<HeartbeatChannel>()
        );
        HeartbeatMonitor = new HeartbeatMonitor(
            responder => HeartbeatChannel.Send(responder),
            () => DateTimeOffset.UtcNow,
            loggerFactory.CreateLogger<HeartbeatMonitor>()
        );
        _onHeartbeatMissedHandler = (_, _) => OnHeartbeatMissed();
        HeartbeatMonitor.HeartbeatMissed += _onHeartbeatMissedHandler;
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }

    public ClientIdentity ClientIdentity { get; }
    
    public CallInvoker CallInvoker { get; }
    
    public PlatformService.PlatformServiceClient Service { get; }
    
    public AsyncDuplexStreamingCallProxy<PlatformInboundInstruction,PlatformOutboundInstruction> InstructionStreamProxy
    {
        get;
    }
    
    public HeartbeatChannel HeartbeatChannel { get; }
    
    public HeartbeatMonitor HeartbeatMonitor { get; }

    private State CurrentState
    {
        get => _state;
        set
        {
            if (_state is not State.Connected && value is State.Connected)
            {
                OnConnected();
            }

            _state = value;
        }
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<PlatformOutboundInstruction> reader, CancellationToken ct)
    {
        await foreach (var response in reader.ReadAllAsync(ct))
        {
            await _inbox.Writer.WriteAsync(new Protocol.ReceivedInstruction(response), ct);
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
                            case State.Paused:
                            case State.Disconnected:
                                var instructionStream = Service.OpenStream(cancellationToken: ct);
                                if (instructionStream != null)
                                {
                                    _logger.LogInformation(
                                        "Connected instruction stream for context '{Context}'. Sending client identification",
                                        _context);
                                    await instructionStream.RequestStream.WriteAsync(new PlatformInboundInstruction
                                    {
                                        Register = ClientIdentity.ToClientIdentification()
                                    });
                                    //TODO: Handle Exceptions
                                    await HeartbeatMonitor.Resume();

                                    CurrentState = new State.Connected(instructionStream, ConsumeResponseStream(instructionStream.ResponseStream, ct));
                                }

                                break;
                            
                            case State.Connected:
                                _logger.LogInformation("ControlChannel for context '{Context}' is already connected", _context.ToString());
                                break;
                        }

                        break;
                    case Protocol.Disconnect:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                await HeartbeatMonitor.Pause();
                                
                                connected.InstructionStream?.Dispose();
                                await connected.ConsumeResponseStreamLoop;

                                CurrentState = new State.Disconnected();
                                break;
                        }

                        break;
                    case Protocol.Reconnect:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                await HeartbeatMonitor.Pause();
                                
                                connected.InstructionStream?.Dispose();
                                await connected.ConsumeResponseStreamLoop;

                                CurrentState = new State.Paused();
                                break;
                        }

                        break;
                    case Protocol.SendAwaitableInstruction send:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                await connected.InstructionStream!.RequestStream.WriteAsync(send.Instruction);
                                send.CompletionSource.SetResult();
                                break;
                            case State.Disconnected:
                                send.CompletionSource.SetException(new AxonServerException(ClientIdentity,
                                    ErrorCategory.InstructionAckError,
                                    "Unable to send instruction: no connection to AxonServer"));
                                break;
                        }

                        break;
                    case Protocol.SendInstruction send:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                await connected.InstructionStream!.RequestStream.WriteAsync(send.Instruction);
                                break;
                            case State.Disconnected:
                                _logger.LogWarning("Unable to send instruction {Instruction}: no connection to AxonServer", send.Instruction);
                                break;
                        }

                        break;
                    case Protocol.ReceivedInstruction received:
                        switch (CurrentState)
                        {
                            case State.Connected connected:
                                switch (received.Instruction.RequestCase)
                                {
                                    case PlatformOutboundInstruction.RequestOneofCase.None:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.NodeNotification:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.RequestReconnect:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.PauseEventProcessor:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.StartEventProcessor:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.ReleaseSegment:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.RequestEventProcessorInfo:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.Heartbeat:
                                        await HeartbeatMonitor.ReceiveServerHeartbeat();
                                        await _inbox.Writer.WriteAsync(new Protocol.SendInstruction(
                                            new PlatformInboundInstruction
                                            {
                                                Heartbeat = new Heartbeat()
                                            }), ct);
                                        break;
                                    case PlatformOutboundInstruction.RequestOneofCase.Ack:
                                        //NOTE: This COULD be an ack for a heartbeat but is not required to be one.
                                        await HeartbeatChannel.Receive(received.Instruction.Ack);
                                        break;
                                }
                                
                                break;
                        }
                        break;
                }
            }
        }
    }
    
    private record Protocol
    {
        public record Connect : Protocol;
        
        public record Disconnect : Protocol;
        
        public record Reconnect : Protocol;

        public record SendInstruction
            (PlatformInboundInstruction Instruction) : Protocol;
        
        public record SendAwaitableInstruction
            (PlatformInboundInstruction Instruction, TaskCompletionSource CompletionSource) : Protocol;

        public record ReceivedInstruction(PlatformOutboundInstruction Instruction) : Protocol;
    }

    private record State(AsyncDuplexStreamingCall<PlatformInboundInstruction,PlatformOutboundInstruction>? InstructionStream)
    {
        public record Disconnected() : 
            State((AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>?)null);
        public record Connected(
            AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> InstructionStream,
            Task ConsumeResponseStreamLoop) :
            State(InstructionStream);
        
        public record Paused() : 
            State((AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>?)null);
    }

    internal async ValueTask Connect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Connect()
        );
    }

    internal async ValueTask Reconnect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Reconnect()
        );
    }
    
    internal async ValueTask Disconnect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Disconnect()
        );
    }
    
    public Task SendInstruction(PlatformInboundInstruction instruction)
    {
        if (instruction == null) throw new ArgumentNullException(nameof(instruction));
        if (string.IsNullOrEmpty(instruction.InstructionId))
        {
            return Task.CompletedTask;
        }
        var completionSource = new TaskCompletionSource();
        if (!_inbox.Writer.TryWrite(
                new Protocol.SendAwaitableInstruction(instruction, completionSource)))
        {
            throw new AxonServerException(
                ClientIdentity,
                ErrorCategory.Other,
                "Unable to send instruction: client could not write to control channel inbox");
        }
        return completionSource.Task;
    }

    public Task EnableHeartbeat(TimeSpan interval, TimeSpan timeout)
    {
        return HeartbeatMonitor.Enable(interval, timeout);
    }

    public Task DisableHeartbeat()
    {
        return HeartbeatMonitor.Disable();
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

    public event EventHandler? HeartbeatMissed;
    
    protected virtual void OnHeartbeatMissed()
    {
        HeartbeatMissed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsConnected => CurrentState is State.Connected;
    
    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion;
        await _protocol;
        HeartbeatMonitor.HeartbeatMissed -= _onHeartbeatMissedHandler;
        await HeartbeatMonitor.DisposeAsync();
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}