using System.Diagnostics.CodeAnalysis;
using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public class ControlChannel : IControlChannel, IAsyncDisposable
{
    private readonly AxonActor<Protocol, State> _actor;
    private readonly Context _context;
    private readonly TimeSpan _eventProcessorUpdateFrequency;
    private readonly ILogger<ControlChannel> _logger;

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
        _eventProcessorUpdateFrequency = eventProcessorUpdateFrequency;
        _logger = loggerFactory.CreateLogger<ControlChannel>();
        _actor = new AxonActor<Protocol, State>(
            Receive,
            new ChannelOwnedState(this, new State.Disconnected(new EventProcessorCollection(), new TaskRunCache(), new TaskRunCache())),
            scheduler,
            _logger
        );
        InstructionStreamProxy =
            new AsyncDuplexStreamingCallProxy<PlatformInboundInstruction, PlatformOutboundInstruction>(
                () => _actor.State.InstructionStream
            );
        HeartbeatChannel = new HeartbeatChannel(
            instruction => _actor.TellAsync(new Protocol.SendPlatformInboundInstruction(instruction), CancellationToken.None),
            () => requestReconnect(),
            scheduler,
            loggerFactory.CreateLogger<HeartbeatChannel>()
        );
    }

    public ClientIdentity ClientIdentity { get; }

    public CallInvoker CallInvoker { get; }

    public RequestReconnect RequestReconnect { get; }

    public PlatformService.PlatformServiceClient Service { get; }

    public AsyncDuplexStreamingCallProxy<PlatformInboundInstruction, PlatformOutboundInstruction> InstructionStreamProxy
    {
        get;
    }

    public HeartbeatChannel HeartbeatChannel { get; }

    private async Task ConsumeResponseStream(IAsyncStreamReader<PlatformOutboundInstruction> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await _actor
                    .TellAsync(new Protocol.ReceivePlatformOutboundInstruction(response), ct)
                    .ConfigureAwait(false);
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception,
                "The control channel instruction stream is no longer being read because an object got disposed");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The control channel instruction stream is no longer being read because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "The control channel instruction stream is no longer being read because of an unexpected exception");
        }
    }

    private async Task<State> Receive(Protocol message, State state, CancellationToken ct)
    {
        switch (message)
        {
            case Protocol.Connect:
                switch (state)
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
                            await HeartbeatChannel.Resume().ConfigureAwait(false);

                            state = new State.Connected(instructionStream,
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

                            await _actor.ScheduleAsync(new Protocol.Connect(), TimeSpan.FromMilliseconds(500), ct);
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
                            await HeartbeatChannel.Resume().ConfigureAwait(false);

                            state = new State.Connected(instructionStream,
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

                            await _actor.ScheduleAsync(new Protocol.Connect(), TimeSpan.FromMilliseconds(500), ct);
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
                switch (state)
                {
                    case State.Connected connected:
                        await HeartbeatChannel.Pause().ConfigureAwait(false);

                        connected.InstructionStream?.Dispose();
                        await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);

                        state = new State.Disconnected(connected.EventProcessors, connected.InstructionTasks,
                            connected.EventProcessorInfoTasks);
                        break;
                }

                break;
            case Protocol.Reconnect:
                switch (state)
                {
                    case State.Connected connected:
                        await HeartbeatChannel.Pause().ConfigureAwait(false);

                        connected.InstructionStream?.Dispose();
                        await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);

                        state = new State.Paused(connected.EventProcessors, connected.InstructionTasks,
                            connected.EventProcessorInfoTasks);
                        break;
                }

                break;
            case Protocol.RegisterEventProcessor register:
                switch (state)
                {
                    case State.Connected connected:
                        connected.EventProcessors.AddEventProcessorInstructionHandler(register.Name, register.Handler);
                        if (connected.EventProcessors.AddEventProcessorInfoSupplier(register.Name,
                                register.InfoSupplier))
                        {
                            await _actor.ScheduleAsync(new Protocol.SendAllEventProcessorInfo(), _eventProcessorUpdateFrequency, ct);
                        }

                        var token = connected.EventProcessorInfoTasks.NextToken();
                        connected.EventProcessorInfoTasks.Add(token, Task.Run(async () =>
                        {
                            try
                            {
                                var info = await register.InfoSupplier();
                                if (info != null)
                                {
                                    await _actor.TellAsync(
                                        new Protocol.SendEventProcessorInfo
                                        (token,
                                            new PlatformInboundInstruction
                                            {
                                                EventProcessorInfo = info
                                            }), ct).ConfigureAwait(false);
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
                        disconnected.EventProcessors.AddEventProcessorInstructionHandler(register.Name,
                            register.Handler);
                        disconnected.EventProcessors.AddEventProcessorInfoSupplier(register.Name,
                            register.InfoSupplier);
                        break;
                }

                register.CompletionSource.SetResult();
                break;
            case Protocol.UnregisterEventProcessor unregister:
                switch (state)
                {
                    case State.Connected connected:
                        connected.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name,
                            unregister.InfoSupplier);
                        connected.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name,
                            unregister.Handler);
                        break;
                    case State.Paused paused:
                        paused.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name,
                            unregister.InfoSupplier);
                        paused.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name,
                            unregister.Handler);
                        break;
                    case State.Disconnected disconnected:
                        disconnected.EventProcessors.RemoveEventProcessorInfoSupplier(unregister.Name,
                            unregister.InfoSupplier);
                        disconnected.EventProcessors.RemoveEventProcessorInstructionHandler(unregister.Name,
                            unregister.Handler);
                        break;
                }

                unregister.CompletionSource.SetResult();
                break;
            case Protocol.SendAllEventProcessorInfo:
                switch (state)
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
                                            await _actor.TellAsync(
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
                        await _actor.ScheduleAsync(new Protocol.SendAllEventProcessorInfo(), _eventProcessorUpdateFrequency, ct);

                        break;
                    case State.Paused:
                    case State.Disconnected:
                        _logger.LogWarning("Not sending processor info for context '{Context}'. Channel not ready...",
                            _context.ToString());
                        break;
                }

                break;
            case Protocol.SendAwaitablePlatformInboundInstruction send:
                switch (state)
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
                switch (state)
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
                if (state.EventProcessorInfoTasks.TryRemove(send.Token, out var eventProcessorInfoTask))
                {
                    await eventProcessorInfoTask; //TODO: This may throw, we need to report on it

                    switch (state)
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
                if (state.InstructionTasks.TryRemove(response.Token, out var instructionTask) &&
                    instructionTask != null)
                {
                    await instructionTask; //TODO: This may throw, we need to report on it

                    switch (state)
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
                switch (state)
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
                                    handler => handler.ReleaseSegment(
                                        new SegmentId(received.Instruction.ReleaseSegment.SegmentIdentifier)),
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
                                                await _actor.TellAsync(
                                                    new Protocol.SendPlatformOutboundInstructionResponse
                                                    (token,
                                                        new PlatformInboundInstruction
                                                        {
                                                            InstructionId =
                                                                received.Instruction.InstructionId,
                                                            EventProcessorInfo = info
                                                        }), ct).ConfigureAwait(false);
                                            }
                                            else
                                            {
                                                await _actor.TellAsync(
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
                                                        }), ct).ConfigureAwait(false);
                                            }
                                        }
                                        catch (Exception exception)
                                        {
                                            await _actor.TellAsync(
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
                                                    }), ct).ConfigureAwait(false);
                                        }
                                    }, ct));
                                }
                                else
                                {
                                    await _actor.TellAsync(
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
                                            }), ct).ConfigureAwait(false);
                                }

                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(
                                        received.Instruction.SplitEventProcessorSegment.ProcessorName),
                                    received.Instruction,
                                    connected.EventProcessors,
                                    connected.InstructionTasks,
                                    handler => handler.SplitSegment(new SegmentId(received.Instruction
                                        .SplitEventProcessorSegment.SegmentIdentifier)),
                                    ct);
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(
                                        received.Instruction.MergeEventProcessorSegment.ProcessorName),
                                    received.Instruction,
                                    connected.EventProcessors,
                                    connected.InstructionTasks,
                                    handler => handler.MergeSegment(new SegmentId(received.Instruction
                                        .MergeEventProcessorSegment.SegmentIdentifier)),
                                    ct);
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.Heartbeat:
                                await HeartbeatChannel.ReceiveServerHeartbeat().ConfigureAwait(false);
                                await _actor.TellAsync(new Protocol.SendPlatformInboundInstruction(
                                    new PlatformInboundInstruction
                                    {
                                        Heartbeat = new Heartbeat()
                                    }), ct).ConfigureAwait(false);
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.Ack:
                                //NOTE: This COULD be an ack for a heartbeat but is not required to be one.
                                await HeartbeatChannel.ReceiveClientHeartbeatAcknowledgement(received.Instruction.Ack)
                                    .ConfigureAwait(false);
                                break;
                        }

                        break;
                }

                break;
        }

        return state;
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
                                await _actor.TellAsync(
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
                                        }), ct).ConfigureAwait(false);
                            }
                            else
                            {
                                await _actor.TellAsync(
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
                                        }), ct).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        await _actor.TellAsync(
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
                                }), ct).ConfigureAwait(false);
                    }
                }, ct));
        }
        else
        {
            if (instruction.InstructionId != null)
            {
                await _actor.TellAsync(
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
                        }), ct).ConfigureAwait(false);
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
        AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>? InstructionStream,
        EventProcessorCollection EventProcessors,
        TaskRunCache InstructionTasks,
        TaskRunCache EventProcessorInfoTasks)
    {
        public record Disconnected(EventProcessorCollection EventProcessors, TaskRunCache InstructionTasks,
            TaskRunCache EventProcessorInfoTasks) :
            State(null, EventProcessors, InstructionTasks, EventProcessorInfoTasks);

        public record Connected(
            AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> InstructionStream,
            Task ConsumeResponseStreamLoop,
            EventProcessorCollection EventProcessors,
            TaskRunCache InstructionTasks,
            TaskRunCache EventProcessorInfoTasks) :
            State(InstructionStream, EventProcessors, InstructionTasks, EventProcessorInfoTasks);

        public record Paused(EventProcessorCollection EventProcessors, TaskRunCache InstructionTasks,
            TaskRunCache EventProcessorInfoTasks) :
            State(null, EventProcessors, InstructionTasks, EventProcessorInfoTasks);
    }

    internal async ValueTask Connect()
    {
        await _actor.TellAsync(
            new Protocol.Connect()
        ).ConfigureAwait(false);
    }

    internal async ValueTask Reconnect()
    {
        await _actor.TellAsync(
            new Protocol.Reconnect()
        ).ConfigureAwait(false);
    }

    internal async ValueTask Disconnect()
    {
        await _actor.TellAsync(
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
        await _actor.TellAsync(new Protocol.RegisterEventProcessor(
            name,
            infoSupplier,
            handler,
            registerCompletionSource), CancellationToken.None).ConfigureAwait(false);
        return new EventProcessorRegistration(registerCompletionSource.Task, async () =>
        {
            var unsubscribeCompletionSource = new TaskCompletionSource();
            await _actor
                .TellAsync(new Protocol.UnregisterEventProcessor(name, infoSupplier, handler, unsubscribeCompletionSource))
                .ConfigureAwait(false);
            await unsubscribeCompletionSource.Task.ConfigureAwait(false);
        });
    }

    public async Task SendInstruction(PlatformInboundInstruction instruction)
    {
        if (instruction == null) throw new ArgumentNullException(nameof(instruction));
        if (!string.IsNullOrEmpty(instruction.InstructionId))
        {
            var completionSource = new TaskCompletionSource();
            await _actor.TellAsync(new Protocol.SendAwaitablePlatformInboundInstruction(instruction, completionSource));
            await completionSource.Task;
        }
    }

    public Task EnableHeartbeat(TimeSpan interval, TimeSpan timeout)
    {
        return HeartbeatChannel.Enable(interval, timeout);
    }

    public Task DisableHeartbeat()
    {
        return HeartbeatChannel.Disable();
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

    public async ValueTask DisposeAsync()
    {
        await HeartbeatChannel.DisposeAsync().ConfigureAwait(false);
        await _actor.DisposeAsync().ConfigureAwait(false);
    }
    
    private class ChannelOwnedState : IAxonActorStateOwner<State>
    {
        private readonly ControlChannel _channel;
        private State _state;

        public ChannelOwnedState(ControlChannel channel, State initialState)
        {
            _channel = channel;
            _state = initialState;
        }

        public State State
        {
            get => _state;
            set
            {
                var connected = _state is not State.Connected && value is State.Connected;
                _state = value;
                if (connected) _channel.OnConnected();
            }
        }
    }
}