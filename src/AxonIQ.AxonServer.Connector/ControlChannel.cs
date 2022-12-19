using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public class ControlChannel : IControlChannel, IAsyncDisposable
{
    private State _state;
    
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    
    private readonly Context _context;
    private readonly IScheduler _scheduler;
    private readonly TimeSpan _eventProcessorUpdateFrequency;
    private readonly ILogger<ControlChannel> _logger;
    private readonly EventHandler _onHeartbeatMissedHandler;

    public ControlChannel(
        ClientIdentity clientIdentity, 
        Context context, 
        CallInvoker callInvoker,
        RequestReconnect requestReconnect,
        IScheduler scheduler,
        TimeSpan eventProcessorUpdateFrequency,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        if (requestReconnect == null) throw new ArgumentNullException(nameof(requestReconnect));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        ClientIdentity = clientIdentity;
        CallInvoker = callInvoker;
        RequestReconnect = requestReconnect;
        Service = new PlatformService.PlatformServiceClient(callInvoker);
        
        _context = context;
        _scheduler = scheduler;
        _eventProcessorUpdateFrequency = eventProcessorUpdateFrequency;
        _logger = loggerFactory.CreateLogger<ControlChannel>();
        _state = new State.Disconnected(new EventProcessorCollection(), new TaskRunCache(), new TaskRunCache());
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
            scheduler.Clock,
            instruction => _inbox.Writer.WriteAsync(new Protocol.SendPlatformInboundInstruction(instruction)),
            loggerFactory.CreateLogger<HeartbeatChannel>()
        );
        HeartbeatMonitor = new HeartbeatMonitor(
            (responder, timeout) => HeartbeatChannel.Send(responder, timeout),
            scheduler.Clock,
            loggerFactory.CreateLogger<HeartbeatMonitor>()
        );
        _onHeartbeatMissedHandler = (_, _) => OnHeartbeatMissed();
        HeartbeatMonitor.HeartbeatMissed += _onHeartbeatMissedHandler;
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }

    public ClientIdentity ClientIdentity { get; }
    
    public CallInvoker CallInvoker { get; }
    
    public RequestReconnect RequestReconnect { get; }

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
            var connected = _state is not State.Connected && value is State.Connected;
            _state = value;
            if (connected) OnConnected();
        }
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<PlatformOutboundInstruction> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await _inbox.Writer.WriteAsync(new Protocol.ReceivePlatformOutboundInstruction(response), ct).ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception, "The instruction stream is no longer being read because an object got disposed");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception, "The instruction stream is no longer being read because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception, "The instruction stream is no longer being read because an operation was cancelled");
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
                                case State.Paused paused:
                                {
                                    var instructionStream = Service.OpenStream(cancellationToken: ct);
                                    if (instructionStream != null)
                                    {
                                        _logger.LogInformation(
                                            "Connected instruction stream for context '{Context}'. Sending client identification",
                                            _context);
                                        await instructionStream.RequestStream.WriteAsync(new PlatformInboundInstruction
                                        {
                                            Register = ClientIdentity.ToClientIdentification()
                                        }).ConfigureAwait(false);
                                        //TODO: Handle Exceptions
                                        await HeartbeatMonitor.Resume().ConfigureAwait(false);

                                        CurrentState = new State.Connected(instructionStream,
                                            ConsumeResponseStream(instructionStream.ResponseStream, ct),
                                            paused.EventProcessors,
                                            paused.InstructionTasks,
                                            paused.EventProcessorInfoTasks
                                        );
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Could not open instruction stream for context '{Context}'",
                                            _context);
                                    }
                                }
                                    break;
                                case State.Disconnected disconnected:
                                {
                                    var instructionStream = Service.OpenStream(cancellationToken: ct);
                                    if (instructionStream != null)
                                    {
                                        _logger.LogInformation(
                                            "Connected instruction stream for context '{Context}'. Sending client identification",
                                            _context);
                                        await instructionStream.RequestStream.WriteAsync(new PlatformInboundInstruction
                                        {
                                            Register = ClientIdentity.ToClientIdentification()
                                        }).ConfigureAwait(false);
                                        //TODO: Handle Exceptions
                                        await HeartbeatMonitor.Resume().ConfigureAwait(false);

                                        CurrentState = new State.Connected(instructionStream,
                                            ConsumeResponseStream(instructionStream.ResponseStream, ct),
                                            disconnected.EventProcessors,
                                            disconnected.InstructionTasks,
                                            disconnected.EventProcessorInfoTasks
                                        );
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Could not open instruction stream for context '{Context}'",
                                            _context);
                                    }
                                }
                                    break;

                                case State.Connected:
                                    _logger.LogInformation(
                                        "ControlChannel for context '{Context}' is already connected",
                                        _context.ToString());
                                    break;
                            }

                            break;
                        case Protocol.Disconnect:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    await HeartbeatMonitor.Pause().ConfigureAwait(false);

                                    connected.InstructionStream?.Dispose();
                                    await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);

                                    CurrentState = new State.Disconnected(connected.EventProcessors, connected.InstructionTasks, connected.EventProcessorInfoTasks);
                                    break;
                            }

                            break;
                        case Protocol.Reconnect:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    await HeartbeatMonitor.Pause().ConfigureAwait(false);

                                    connected.InstructionStream?.Dispose();
                                    await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);

                                    CurrentState = new State.Paused(connected.EventProcessors, connected.InstructionTasks, connected.EventProcessorInfoTasks);
                                    break;
                            }

                            break;
                        case Protocol.RegisterEventProcessor register:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    connected.EventProcessors.AddEventProcessorInstructionHandler(register.Name, register.Handler);
                                    if (connected.EventProcessors.AddEventProcessorInfoSupplier(register.Name,
                                            register.InfoSupplier))
                                    {
                                        await _scheduler.ScheduleTask(
                                            () => _inbox.Writer.WriteAsync(new Protocol.SendAllEventProcessorInfo(), ct),
                                            _scheduler.Clock().Add(_eventProcessorUpdateFrequency));
                                    }
                                    
                                    var token = connected.EventProcessorInfoTasks.NextToken();
                                    connected.EventProcessorInfoTasks.Add(token, Task.Run(async () =>
                                    {
                                        try
                                        {

                                            var info = await register.InfoSupplier();
                                            if (info != null)
                                            {
                                                await _inbox.Writer.WriteAsync(
                                                    new Protocol.SendEventProcessorInfo
                                                    (token,
                                                        new PlatformInboundInstruction
                                                        {
                                                            EventProcessorInfo = info
                                                        })).ConfigureAwait(false);
                                            }
                                            else
                                            {
                                                _logger.LogWarning(
                                                    "Not sending processor info {Name} for context '{Context}'.",
                                                    register.Name.ToString(),
                                                    _context.ToString());
                                            }
                                        }
                                        catch (Exception exception)
                                        {
                                            _logger.LogError(exception,
                                                "Not sending processor info for context '{Context}'.",
                                                _context.ToString());
                                        }
                                    }, ct));
                                    
                                    break;
                                case State.Paused paused:
                                    //TODO: We should schedule sending all event processor info as soon as we reconnect 
                                    paused.EventProcessors.AddEventProcessorInstructionHandler(register.Name, register.Handler);
                                    paused.EventProcessors.AddEventProcessorInfoSupplier(register.Name,
                                        register.InfoSupplier);
                                    break;
                                case State.Disconnected disconnected:
                                    //TODO: We should schedule sending all event processor info as soon as we reconnect
                                    disconnected.EventProcessors.AddEventProcessorInstructionHandler(register.Name, register.Handler);
                                    disconnected.EventProcessors.AddEventProcessorInfoSupplier(register.Name, register.InfoSupplier);
                                    break;
                            }
                            register.CompletionSource.SetResult();
                            break;
                        case Protocol.UnregisterEventProcessor unregister:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    connected.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name, unregister.InfoSupplier);
                                    connected.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name, unregister.Handler);
                                    break;
                                case State.Paused paused:
                                    paused.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name, unregister.InfoSupplier);
                                    paused.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name, unregister.Handler);
                                    break;
                                case State.Disconnected disconnected:
                                    disconnected.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name, unregister.InfoSupplier);
                                    disconnected.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name, unregister.Handler);
                                    break;
                            }
                            unregister.CompletionSource.SetResult();
                            break;
                        case Protocol.SendAllEventProcessorInfo:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    foreach (var (name, supplier) in connected.EventProcessors
                                                 .GetAllEventProcessorInfoSuppliers())
                                    {
                                        var token = connected.EventProcessorInfoTasks.NextToken();
                                        connected.EventProcessorInfoTasks.Add(token,
                                            Task.Run(async () =>
                                            {
                                                try
                                                {
                                                    var info = await supplier().ConfigureAwait(false);
                                                    if (info != null)
                                                    {
                                                        await _inbox.Writer.WriteAsync(
                                                            new Protocol.SendEventProcessorInfo(
                                                                token,
                                                                new PlatformInboundInstruction
                                                                {
                                                                    EventProcessorInfo = info
                                                                }), ct).ConfigureAwait(false);
                                                    }
                                                    else
                                                    {
                                                        _logger.LogWarning(
                                                            "Not sending processor info {Name} for context '{Context}'.",
                                                            name.ToString(),
                                                            _context.ToString());
                                                    }
                                                }
                                                catch (Exception exception)
                                                {
                                                    _logger.LogError(exception,
                                                        "Not sending processor info for context '{Context}'.",
                                                        _context.ToString());
                                                }
                                            }, ct));
                                    }
                                    
                                    // Schedule the next poll
                                    await _scheduler.ScheduleTask(
                                        () => _inbox.Writer.WriteAsync(new Protocol.SendAllEventProcessorInfo(), ct),
                                        _scheduler.Clock().Add(_eventProcessorUpdateFrequency));
                                    
                                    break;
                                case State.Paused:
                                case State.Disconnected:
                                    _logger.LogWarning("Not sending processor info for context '{Context}'. Channel not ready...", _context.ToString());
                                    break;
                            }

                            break;
                        case Protocol.SendAwaitablePlatformInboundInstruction send:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    try
                                    {
                                        await connected.InstructionStream!.RequestStream.WriteAsync(send.Instruction)
                                            .ConfigureAwait(false);
                                        send.CompletionSource.SetResult();
                                    }
                                    catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                                    {
                                        send.CompletionSource.SetException(new AxonServerException(ClientIdentity,
                                            ErrorCategory.InstructionAckError,
                                            "Unable to send instruction: AxonServer unavailable"));
                                    }

                                    break;
                                case State.Disconnected:
                                    send.CompletionSource.SetException(new AxonServerException(ClientIdentity,
                                        ErrorCategory.InstructionAckError,
                                        "Unable to send instruction: no connection to AxonServer"));
                                    break;
                            }

                            break;
                        case Protocol.SendPlatformInboundInstruction send:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    try
                                    {
                                        await connected.InstructionStream!.RequestStream.WriteAsync(send.Instruction)
                                            .ConfigureAwait(false);
                                    }
                                    catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                                    {
                                        _logger.LogWarning(
                                            exception,
                                            "Unable to send instruction {Instruction}: AxonServer unavailable",
                                            send.Instruction);    
                                    }

                                    break;
                                case State.Disconnected:
                                    _logger.LogWarning(
                                        "Unable to send instruction {Instruction}: no connection to AxonServer",
                                        send.Instruction);
                                    break;
                            }

                            break;
                        case Protocol.SendEventProcessorInfo send:
                            if (CurrentState.EventProcessorInfoTasks.TryRemove(send.Token, out var eventProcessorInfoTask))
                            {
                                await eventProcessorInfoTask; //TODO: This may throw, we need to report on it
                                
                                switch (CurrentState)
                                {
                                    case State.Connected connected:
                                        try
                                        {
                                            await connected.InstructionStream!.RequestStream
                                                .WriteAsync(send.Instruction)
                                                .ConfigureAwait(false);
                                        }
                                        catch (RpcException exception) when (exception.StatusCode ==
                                                                             StatusCode.Unavailable)
                                        {
                                            _logger.LogWarning(
                                                exception,
                                                "Unable to send instruction {Instruction}: AxonServer unavailable",
                                                send.Instruction);
                                        }   

                                        break;
                                    case State.Disconnected:
                                        _logger.LogWarning(
                                            "Unable to send instruction {Instruction}: no connection to AxonServer",
                                            send.Instruction);
                                        break;
                                }
                            }
                            break;
                        case Protocol.SendPlatformOutboundInstructionResponse response:
                            if (CurrentState.InstructionTasks.TryRemove(response.Token, out var instructionTask))
                            {
                                await instructionTask; //TODO: This may throw, we need to report on it
                                
                                switch (CurrentState)
                                {
                                    case State.Connected connected:
                                        try
                                        {
                                            await connected.InstructionStream!.RequestStream
                                                .WriteAsync(response.Instruction)
                                                .ConfigureAwait(false);
                                        }
                                        catch (RpcException exception) when (exception.StatusCode ==
                                                                             StatusCode.Unavailable)
                                        {
                                            _logger.LogWarning(
                                                exception,
                                                "Unable to send instruction {Instruction}: AxonServer unavailable",
                                                response.Instruction);
                                        }   

                                        break;
                                    case State.Disconnected:
                                        _logger.LogWarning(
                                            "Unable to send instruction {Instruction}: no connection to AxonServer",
                                            response.Instruction);
                                        break;
                                }
                            }
                            break;
                        case Protocol.ReceivePlatformOutboundInstruction received:
                            switch (CurrentState)
                            {
                                case State.Connected connected:
                                    switch (received.Instruction.RequestCase)
                                    {
                                        // case PlatformOutboundInstruction.RequestOneofCase.None:
                                        //     break;
                                        // case PlatformOutboundInstruction.RequestOneofCase.NodeNotification:
                                        //     break;
                                        case PlatformOutboundInstruction.RequestOneofCase.RequestReconnect:
                                            await RequestReconnect().ConfigureAwait(false);
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.PauseEventProcessor:
                                            await ExecuteEventProcessorInstruction(
                                                new EventProcessorName(received.Instruction.PauseEventProcessor.ProcessorName),
                                                received.Instruction,
                                                connected.EventProcessors, 
                                                connected.InstructionTasks,
                                                async handler =>
                                                {
                                                    await handler.Pause();
                                                    return true;
                                                }, 
                                                ct);

                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.StartEventProcessor:
                                            await ExecuteEventProcessorInstruction(
                                                new EventProcessorName(received.Instruction.StartEventProcessor.ProcessorName),
                                                received.Instruction,
                                                connected.EventProcessors, 
                                                connected.InstructionTasks,
                                                async handler =>
                                                {
                                                    await handler.Start();
                                                    return true;
                                                }, 
                                                ct);
                                        
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.ReleaseSegment:
                                            await ExecuteEventProcessorInstruction(
                                                new EventProcessorName(received.Instruction.ReleaseSegment.ProcessorName),
                                                received.Instruction,
                                                connected.EventProcessors, 
                                                connected.InstructionTasks,
                                                handler => handler.ReleaseSegment(new SegmentId(received.Instruction.ReleaseSegment.SegmentIdentifier)), 
                                                ct);
                                        
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.RequestEventProcessorInfo:
                                            var name = new EventProcessorName(
                                                received.Instruction.RequestEventProcessorInfo.ProcessorName);
                                            var supplier =
                                                connected.EventProcessors.GetEventProcessorInfoSupplier(name);
                                            if (supplier != null)
                                            {
                                                var token = connected.InstructionTasks.NextToken();
                                                connected.InstructionTasks.Add(token, Task.Run(async () =>
                                                {
                                                    try
                                                    {

                                                        var info = await supplier();
                                                        if (info != null)
                                                        {
                                                            await _inbox.Writer.WriteAsync(
                                                                new Protocol.SendPlatformOutboundInstructionResponse
                                                                (token,
                                                                    new PlatformInboundInstruction
                                                                    {
                                                                        InstructionId =
                                                                            received.Instruction.InstructionId,
                                                                        EventProcessorInfo = info
                                                                    })).ConfigureAwait(false);
                                                        }
                                                        else
                                                        {
                                                            await _inbox.Writer.WriteAsync(
                                                                new Protocol.SendPlatformInboundInstruction(
                                                                    new PlatformInboundInstruction
                                                                    {
                                                                        Result = new InstructionResult
                                                                        {
                                                                            InstructionId = received.Instruction
                                                                                .InstructionId,
                                                                            Success = false,
                                                                            Error = new ErrorMessage
                                                                            {
                                                                                ErrorCode = ErrorCategory
                                                                                    .InstructionExecutionError
                                                                                    .ToString(),
                                                                                Location =
                                                                                    ClientIdentity.ClientInstanceId
                                                                                        .ToString(),
                                                                                Message = "Unknown processor"
                                                                            }
                                                                        }

                                                                    })).ConfigureAwait(false);
                                                        }
                                                    }
                                                    catch (Exception exception)
                                                    {
                                                        await _inbox.Writer.WriteAsync(
                                                            new Protocol.SendPlatformOutboundInstructionResponse
                                                            (token,
                                                                new PlatformInboundInstruction
                                                                {
                                                                    Result = new InstructionResult
                                                                    {
                                                                        InstructionId = received.Instruction
                                                                            .InstructionId,
                                                                        Success = false,
                                                                        Error = new ErrorMessage
                                                                        {
                                                                            ErrorCode = ErrorCategory
                                                                                .InstructionExecutionError
                                                                                .ToString(),
                                                                            Location = ClientIdentity
                                                                                .ClientInstanceId
                                                                                .ToString(),
                                                                            Message = exception.Message,
                                                                            Details =
                                                                            {
                                                                                exception.ToString()
                                                                            }
                                                                        }
                                                                    }
                                                                })).ConfigureAwait(false);
                                                    }
                                                }, ct));
                                            }
                                            else
                                            {
                                                await _inbox.Writer.WriteAsync(
                                                    new Protocol.SendPlatformInboundInstruction(
                                                        new PlatformInboundInstruction
                                                        {
                                                            Result = new InstructionResult
                                                            {
                                                                InstructionId = received.Instruction.InstructionId,
                                                                Success = false,
                                                                Error = new ErrorMessage
                                                                {
                                                                    ErrorCode = ErrorCategory.InstructionExecutionError
                                                                        .ToString(),
                                                                    Location =
                                                                        ClientIdentity.ClientInstanceId.ToString(),
                                                                    Message = "Unknown processor"
                                                                }
                                                            }

                                                        })).ConfigureAwait(false);
                                            }
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
                                            await ExecuteEventProcessorInstruction(
                                                new EventProcessorName(received.Instruction.SplitEventProcessorSegment.ProcessorName),
                                                received.Instruction,
                                                connected.EventProcessors, 
                                                connected.InstructionTasks,
                                                handler => handler.SplitSegment(new SegmentId(received.Instruction.SplitEventProcessorSegment.SegmentIdentifier)), 
                                                ct);
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
                                            await ExecuteEventProcessorInstruction(
                                                new EventProcessorName(received.Instruction.MergeEventProcessorSegment.ProcessorName),
                                                received.Instruction,
                                                connected.EventProcessors, 
                                                connected.InstructionTasks,
                                                handler => handler.MergeSegment(new SegmentId(received.Instruction.MergeEventProcessorSegment.SegmentIdentifier)), 
                                                ct);
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.Heartbeat:
                                            await HeartbeatMonitor.ReceiveServerHeartbeat().ConfigureAwait(false);
                                            await _inbox.Writer.WriteAsync(new Protocol.SendPlatformInboundInstruction(
                                                new PlatformInboundInstruction
                                                {
                                                    Heartbeat = new Heartbeat()
                                                })).ConfigureAwait(false);
                                            break;
                                        case PlatformOutboundInstruction.RequestOneofCase.Ack:
                                            //NOTE: This COULD be an ack for a heartbeat but is not required to be one.
                                            await HeartbeatChannel.Receive(received.Instruction.Ack).ConfigureAwait(false);
                                            break;
                                    }

                                    break;
                            }

                            break;
                    }

                    _logger.LogDebug("Completed {Message} with {State}", message.ToString(), CurrentState.ToString());
                }
            }
        }
        catch (RpcException exception) when (exception.Status.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug(exception,
                "Control channel message loop is exciting because an rpc call was cancelled");
        }
        catch (TaskCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Control channel message loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Control channel message loop is exciting because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Control channel message pump is exciting because of an unexpected exception");
        }
    }

    private async Task ExecuteEventProcessorInstruction(
        EventProcessorName name,
        PlatformOutboundInstruction instruction, 
        EventProcessorCollection eventProcessors,
        TaskRunCache instructionTasks,
        Func<IEventProcessorInstructionHandler, Task<bool>> execute,
        CancellationToken ct)
    {
        var handler = eventProcessors
            .TryResolveEventProcessorInstructionHandler(name);
        if (handler != null)
        {
            var token = instructionTasks.NextToken();
            instructionTasks.Add(token,
                Task.Run(async () =>
                {
                    try
                    {
                        var result = await execute(handler);
                        if (instruction.InstructionId != null)
                        {
                            if (result)
                            {
                                await _inbox.Writer.WriteAsync(
                                    new Protocol.SendPlatformOutboundInstructionResponse
                                    (token,
                                        new PlatformInboundInstruction
                                        {
                                            Result = new InstructionResult
                                            {
                                                InstructionId =
                                                    instruction.InstructionId,
                                                Success = true
                                            }
                                        })).ConfigureAwait(false);
                            }
                            else
                            {
                                await _inbox.Writer.WriteAsync(
                                    new Protocol.SendPlatformOutboundInstructionResponse
                                    (token,
                                        new PlatformInboundInstruction
                                        {
                                            Result = new InstructionResult
                                            {
                                                InstructionId = instruction
                                                    .InstructionId,
                                                Success = false,
                                                Error = new ErrorMessage
                                                {
                                                    ErrorCode = ErrorCategory
                                                        .InstructionExecutionError
                                                        .ToString(),
                                                    Location = ClientIdentity
                                                        .ClientInstanceId
                                                        .ToString()
                                                }
                                            }
                                        })).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        await _inbox.Writer.WriteAsync(
                            new Protocol.SendPlatformOutboundInstructionResponse
                            (token,
                                new PlatformInboundInstruction
                                {
                                    Result = new InstructionResult
                                    {
                                        InstructionId = instruction
                                            .InstructionId,
                                        Success = false,
                                        Error = new ErrorMessage
                                        {
                                            ErrorCode = ErrorCategory
                                                .InstructionExecutionError
                                                .ToString(),
                                            Location = ClientIdentity
                                                .ClientInstanceId
                                                .ToString(),
                                            Message = exception.Message,
                                            Details =
                                            {
                                                exception.ToString()
                                            }
                                        }
                                    }
                                })).ConfigureAwait(false);
                    }
                }, ct));
        }
        else
        {
            if (instruction.InstructionId != null)
            {
                await _inbox.Writer.WriteAsync(
                    new Protocol.SendPlatformInboundInstruction(
                        new PlatformInboundInstruction
                        {
                            Result = new InstructionResult
                            {
                                InstructionId = instruction.InstructionId,
                                Success = false,
                                Error = new ErrorMessage
                                {
                                    ErrorCode = ErrorCategory.InstructionExecutionError
                                        .ToString(),
                                    Location =
                                        ClientIdentity.ClientInstanceId.ToString(),
                                    Message = "Unknown processor"
                                }
                            }

                        })).ConfigureAwait(false);
            }
        }
    }
    
    private record Protocol
    {
        public record Connect : Protocol;
        
        public record Disconnect : Protocol;
        
        public record Reconnect : Protocol;

        public record SendPlatformInboundInstruction
            (PlatformInboundInstruction Instruction) : Protocol;
        
        public record SendEventProcessorInfo
            (long Token, PlatformInboundInstruction Instruction) : Protocol;
        
        public record SendAllEventProcessorInfo() : Protocol;
        
        public record SendAwaitablePlatformInboundInstruction
            (PlatformInboundInstruction Instruction, TaskCompletionSource CompletionSource) : Protocol;
        
        public record SendPlatformOutboundInstructionResponse
            (long Token, PlatformInboundInstruction Instruction) : Protocol;

        public record ReceivePlatformOutboundInstruction(PlatformOutboundInstruction Instruction) : Protocol;

        public record RegisterEventProcessor(EventProcessorName Name, Func<Task<EventProcessorInfo>> InfoSupplier,
            IEventProcessorInstructionHandler Handler, TaskCompletionSource CompletionSource) : Protocol;
        public record UnregisterEventProcessor(EventProcessorName Name, Func<Task<EventProcessorInfo>> InfoSupplier,
            IEventProcessorInstructionHandler Handler, TaskCompletionSource CompletionSource) : Protocol;
    }

    private record State(
        AsyncDuplexStreamingCall<PlatformInboundInstruction,PlatformOutboundInstruction>? InstructionStream, 
        EventProcessorCollection EventProcessors,
        TaskRunCache InstructionTasks,
        TaskRunCache EventProcessorInfoTasks)
    {
        public record Disconnected(EventProcessorCollection EventProcessors, TaskRunCache InstructionTasks, TaskRunCache EventProcessorInfoTasks) : 
            State(null, EventProcessors, InstructionTasks, EventProcessorInfoTasks);
        public record Connected(
            AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> InstructionStream,
            Task ConsumeResponseStreamLoop,
            EventProcessorCollection EventProcessors, 
            TaskRunCache InstructionTasks,
            TaskRunCache EventProcessorInfoTasks) :
            State(InstructionStream, EventProcessors, InstructionTasks, EventProcessorInfoTasks);
        
        public record Paused(EventProcessorCollection EventProcessors, TaskRunCache InstructionTasks,TaskRunCache EventProcessorInfoTasks) : 
            State(null, EventProcessors, InstructionTasks, EventProcessorInfoTasks);
    }

    internal async ValueTask Connect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Connect()
        ).ConfigureAwait(false);
    }

    internal async ValueTask Reconnect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Reconnect()
        ).ConfigureAwait(false);
    }
    
    internal async ValueTask Disconnect()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Disconnect()
        ).ConfigureAwait(false);
    }

    public async Task<IEventProcessorRegistration> RegisterEventProcessor(
        EventProcessorName name, 
        Func<Task<EventProcessorInfo>> infoSupplier,
        IEventProcessorInstructionHandler handler)
    {
        if (infoSupplier == null) throw new ArgumentNullException(nameof(infoSupplier));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var registerCompletionSource = new TaskCompletionSource();
        await _inbox.Writer.WriteAsync(new Protocol.RegisterEventProcessor(
            name,
            infoSupplier,
            handler,
            registerCompletionSource)).ConfigureAwait(false);
        return new EventProcessorRegistration(registerCompletionSource.Task, async () =>
        {
            var unsubscribeCompletionSource = new TaskCompletionSource();
            await _inbox.Writer.WriteAsync(
                new Protocol.UnregisterEventProcessor(name, infoSupplier, handler, unsubscribeCompletionSource)).ConfigureAwait(false);
            await unsubscribeCompletionSource.Task.ConfigureAwait(false);
        });
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
                new Protocol.SendAwaitablePlatformInboundInstruction(instruction, completionSource)))
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
        await _inbox.Reader.Completion.ConfigureAwait(false);
        await _protocol.ConfigureAwait(false);
        HeartbeatMonitor.HeartbeatMissed -= _onHeartbeatMissedHandler;
        await HeartbeatMonitor.DisposeAsync().ConfigureAwait(false);
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}