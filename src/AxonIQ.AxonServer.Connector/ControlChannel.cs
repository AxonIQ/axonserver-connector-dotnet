using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class ControlChannel : IControlChannel, IAsyncDisposable
{
    private readonly IOwnerAxonServerConnection _connection;
    private readonly TimeSpan _eventProcessorUpdateFrequency;
    private readonly ILogger<ControlChannel> _logger;
    private readonly AxonActor<Message, State> _actor;

    public ControlChannel(
        IOwnerAxonServerConnection connection,
        IScheduler scheduler,
        TimeSpan eventProcessorUpdateFrequency,
        ILoggerFactory loggerFactory)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        _connection = connection;
        _eventProcessorUpdateFrequency = eventProcessorUpdateFrequency;
        _logger = loggerFactory.CreateLogger<ControlChannel>();
        
        Service = new PlatformService.PlatformServiceClient(connection.CallInvoker);
        
        _actor = new AxonActor<Message, State>(
            Receive,
            new State.Disconnected(new EventProcessorCollection()),
            scheduler,
            _logger
        );
        HeartbeatChannel = new HeartbeatChannel(
            instruction => _actor.TellAsync(new Message.SendPlatformInboundInstruction(instruction), CancellationToken.None),
            connection.ReconnectAsync,
            scheduler,
            loggerFactory.CreateLogger<HeartbeatChannel>()
        );
    }

    public PlatformService.PlatformServiceClient Service { get; }

    public HeartbeatChannel HeartbeatChannel { get; }

#pragma warning disable CA2016
#pragma warning disable CS4014
// ReSharper disable MethodSupportsCancellation
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected, Message.Connect):
            case (State.Disconnected, Message.Reconnect):
                await _actor.TellAsync(new Message.OpenStream(), ct);

                state = new State.Connecting.StreamClosed(state.EventProcessors);
                break;
            case (_, Message.PauseHeartbeats):
                await HeartbeatChannel.Pause().ConfigureAwait(false);
                break;
            case (State.Reconnecting.StreamClosed, Message.OpenStream):
                await _actor.TellToAsync(
                    () => Service.OpenStream(cancellationToken: ct),
                    result => new Message.StreamOpened(result),
                    ct);    
                
                break;
            case (State.Reconnecting.StreamClosed closed, Message.StreamOpened opened):
                switch (opened.Result)
                {
                    case TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>.Ok ok:
                        await _actor.TellAsync(new Message.AnnounceClient(), ct);

                        state = new State.Reconnecting.StreamOpened(ok.Value, closed.EventProcessors);
                        
                        break;
                    case TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);
                        
                        break;
                }

                break;
            case (State.Reconnecting.StreamOpened opened, Message.AnnounceClient):
                opened.Call.RequestStream.WriteAsync(new PlatformInboundInstruction
                {
                    Register = _connection.ClientIdentity.ToClientIdentification()
                }).TellToAsync(
                    _actor,
                    result => new Message.ClientAnnounced(result),
                    ct);

                break;
            case (State.Reconnecting.StreamOpened opened, Message.ClientAnnounced announced):
                switch (announced.Result)
                {
                    case TaskResult.Ok:
                        await _actor.TellAsync(new Message[]
                        {
                            new Message.ResumeHeartbeats(), 
                            new Message.ResumeEventProcessors()
                        }, ct);
                        
                        var consumerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var consumer = opened.Call
                            .ResponseStream
                            .TellToAsync(
                                _actor,
                                result => new Message.ReceivePlatformOutboundInstruction(result),
                                _logger,
                                consumerTokenSource.Token);

                        await _actor.TellAsync(new Message.OnConnected(), ct);
                        
                        state = new State.Connected(
                            opened.Call, 
                            consumer, 
                            consumerTokenSource, 
                            new Dictionary<InstructionId, TaskCompletionSource>(), 
                            opened.EventProcessors);
                        
                        break;
                    case TaskResult.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to announce client. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        try
                        {
                            opened.Call.Dispose();
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                        }

                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);

                        state = new State.Reconnecting.StreamClosed(opened.EventProcessors);
                        break;
                }
                break;
            case (State.Connecting.StreamClosed, Message.OpenStream):
                await _actor.TellToAsync(
                    () => Service.OpenStream(cancellationToken: ct),
                    result => new Message.StreamOpened(result),
                    ct);

                break;
            case (State.Connecting.StreamClosed closed, Message.StreamOpened opened):
                switch (opened.Result)
                {
                    case TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>.Ok ok:
                        await _actor.TellAsync(new Message.AnnounceClient(), ct);

                        state = new State.Connecting.StreamOpened(ok.Value, closed.EventProcessors);
                        
                        break;
                    case TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);
                        
                        break;
                }

                break;
            case (State.Connecting.StreamOpened opened, Message.AnnounceClient):
                opened.Call.RequestStream.WriteAsync(new PlatformInboundInstruction
                {
                    Register = _connection.ClientIdentity.ToClientIdentification()
                }).TellToAsync(
                    _actor,
                    result => new Message.ClientAnnounced(result),
                    ct);

                break;
            case (State.Connecting.StreamOpened opened, Message.ClientAnnounced announced):
                switch (announced.Result)
                {
                    case TaskResult.Ok:
                        await _actor.TellAsync(new Message[]
                        {
                            new Message.ResumeHeartbeats(), 
                            new Message.ResumeEventProcessors()
                        }, ct);
                        
                        var consumerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var consumer = opened.Call
                            .ResponseStream
                            .TellToAsync(
                                _actor,
                                result => new Message.ReceivePlatformOutboundInstruction(result),
                                _logger,
                                consumerTokenSource.Token);
                        
                        await _actor.TellAsync(new Message.OnConnected(), ct);

                        state = new State.Connected(
                            opened.Call, 
                            consumer, 
                            consumerTokenSource,
                            new Dictionary<InstructionId, TaskCompletionSource>(),
                            opened.EventProcessors);
                        
                        break;
                    case TaskResult.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to announce client. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        try
                        {
                            opened.Call.Dispose();
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                        }

                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);

                        state = new State.Connecting.StreamClosed(opened.EventProcessors);
                        break;
                }
                break;
            case (State.Connected connected, Message.Disconnect):
                try
                {
                    connected.Call.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                }

                state = new State.Disconnected(connected.EventProcessors);
                    
                break;
            case (State.Connecting.StreamOpened opened, Message.Disconnect):
                try
                {
                    opened.Call.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                }

                state = new State.Disconnected(opened.EventProcessors);
                    
                break;
            case (State.Reconnecting.StreamOpened opened, Message.Disconnect):
                try
                {
                    opened.Call.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                }

                state = new State.Disconnected(opened.EventProcessors);
                    
                break;
            case (State.Connected, Message.ResumeHeartbeats):
                await HeartbeatChannel.Resume().ConfigureAwait(false);
                    
                break;
            case (State.Connected connected, Message.ResumeEventProcessors):
                if (connected.EventProcessors.Count != 0)
                {
                    await _actor.TellAsync(new Message.SendAllEventProcessorInfo(), ct);
                }

                break;
            case (State.Connected connected, Message.SendAllEventProcessorInfo):
                var suppliers = connected.EventProcessors.GetAllEventProcessorInfoSuppliers();
                if (suppliers.Count != 0)
                {
                    foreach (var (name, supplier) in suppliers)
                    {
                        supplier
                            .TellToAsync(
                                _actor,
                                result => new Message.GotEventProcessorInfo(name, InstructionId.New(), result),
                                ct);
                    }
                    
                    await _actor.ScheduleAsync(new Message.SendAllEventProcessorInfo(), _eventProcessorUpdateFrequency, ct);
                }

                break;
            case (State.Connected, Message.GotEventProcessorInfo gotten):
                switch (gotten.Result)
                {
                    case TaskResult<EventProcessorInfo?>.Ok ok:
                        if (ok.Value != null)
                        {
                            await _actor.TellAsync(
                                new Message.SendPlatformInboundInstruction(
                                    new PlatformInboundInstruction
                                    {
                                        EventProcessorInfo = ok.Value 
                                    })
                                , ct);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Not sending processor info {Name} for context '{Context}'",
                                gotten.Name.ToString(),
                                _connection.Context.ToString());
                        }
                        break;
                    case TaskResult<EventProcessorInfo?>.Error error:
                        _logger.LogError(
                            error.Exception,
                            "Not sending processor info {Name} for context '{Context}'",
                            gotten.Name.ToString(),
                            _connection.Context.ToString());
                        break;
                }
                break;
            case (State.Connected connected, Message.SendPlatformInboundInstruction send):
                //NOTE: We do want to await here because the write on the request stream must complete before continuing.
                await connected
                    .Call
                    .RequestStream
                    .WriteAsync(send.Instruction)
                    .TellToAsync(
                        _actor,
                        exception => new Message.SendPlatformInboundInstructionFaulted(send.Instruction, exception),
                        ct);
                break;
            case (_, Message.SendPlatformInboundInstruction send):
                _logger.LogWarning(
                    "Unable to send instruction {Instruction}: no connection to AxonServer",
                    send.Instruction);
                break;

            case (State.Connected, Message.SendPlatformInboundInstructionFaulted faulted):
            {
                var due = ScheduleDue.FromException(faulted.Exception);

                _logger.LogError(
                    faulted.Exception,
                    "Unable to send instruction {Instruction}. Reconnecting in {Due}ms",
                    faulted.Instruction,
                    due.TotalMilliseconds);

                await _actor.ScheduleAsync(new Message.Reconnect(), due, ct);
            }
                break;
            case (State.Connected connected, Message.SendAwaitablePlatformInboundInstruction send):
                try
                {
                    await connected.Call.RequestStream.WriteAsync(send.Instruction).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(send.Instruction.InstructionId))
                    {
                        connected.AwaitedInstructions.Add(InstructionId.Parse(send.Instruction.InstructionId), send.Completion);
                    }
                    if (string.IsNullOrEmpty(send.Instruction.InstructionId))
                    {
                        send.Completion.SetResult();
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unable to send instruction {Instruction}",
                        send.Instruction);
                    
                    send.Completion.SetException(
                        new AxonServerException(
                            _connection.ClientIdentity,
                            ErrorCategory.InstructionAckError,
                            "Unable to send instruction: AxonServer unavailable"));
                }
                break;
            case (_, Message.SendAwaitablePlatformInboundInstruction send):
                send.Completion.SetException(
                    new AxonServerException(
                        _connection.ClientIdentity,
                        ErrorCategory.InstructionAckError,
                        "Unable to send instruction: no connection to AxonServer"));
                break;
            case (State.Connected connected, Message.ReceivePlatformOutboundInstruction received):
                switch (received.Result)
                {
                    case TaskResult<PlatformOutboundInstruction>.Ok ok:
                        switch (ok.Value.RequestCase)
                        {
                            // case PlatformOutboundInstruction.RequestOneofCase.NodeNotification:
                            //     break;
                            case PlatformOutboundInstruction.RequestOneofCase.RequestReconnect:
                                _logger.LogInformation("AxonServer requested reconnect for context '{Context}'", _connection.Context);
                                await _connection.ReconnectAsync().ConfigureAwait(false);
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.PauseEventProcessor:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.PauseEventProcessor.ProcessorName),
                                    async handler =>
                                    {
                                        await handler.PauseAsync();
                                        return true;
                                    },
                                    InstructionId.Parse(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.StartEventProcessor:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.StartEventProcessor.ProcessorName),
                                    async handler =>
                                    {
                                        await handler.StartAsync();
                                        return true;
                                    },
                                    InstructionId.Parse(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.ReleaseSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.ReleaseSegment.ProcessorName),
                                    handler => handler.ReleaseSegmentAsync(new SegmentId(ok.Value.ReleaseSegment.SegmentIdentifier)),
                                    InstructionId.Parse(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.RequestEventProcessorInfo:
                                var name = new EventProcessorName(ok.Value.RequestEventProcessorInfo.ProcessorName);
                                if (connected.EventProcessors.TryGetEventProcessorInfoSupplier(name, out var supplier))
                                {
                                    supplier?
                                        .TellToAsync(
                                            _actor,
                                            result => new Message.GotEventProcessorInfo(
                                                name,
                                                InstructionId.Parse(ok.Value.InstructionId),
                                                result),
                                            ct
                                        );
                                }
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.SplitEventProcessorSegment.ProcessorName),
                                    handler => handler.SplitSegmentAsync(new SegmentId(ok.Value.SplitEventProcessorSegment.SegmentIdentifier)),
                                    InstructionId.Parse(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.MergeEventProcessorSegment.ProcessorName),
                                    handler => handler.MergeSegmentAsync(new SegmentId(ok.Value.MergeEventProcessorSegment.SegmentIdentifier)),
                                    InstructionId.Parse(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.Heartbeat:
                                await HeartbeatChannel.ReceiveServerHeartbeat().ConfigureAwait((false));
                                await _actor.TellAsync(new Message.SendPlatformInboundInstruction(
                                    new PlatformInboundInstruction
                                    {
                                        Heartbeat = new Heartbeat()
                                    }), ct).ConfigureAwait(false);
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.Ack:
                                if(InstructionId.TryParse(ok.Value.Ack.InstructionId, out var instructionId)) 
                                {
                                    if (connected.AwaitedInstructions.TryGetValue(instructionId, out var completion))
                                    {
                                        connected.AwaitedInstructions.Remove(instructionId);
                                        if (ok.Value.Ack.Success)
                                        {
                                            completion.SetResult();
                                        }
                                        else
                                        {
                                            completion.SetException(
                                                AxonServerException.FromErrorMessage(
                                                    _connection.ClientIdentity,
                                                    ok.Value.Ack.Error));
                                        }
                                    }
                                    else
                                    {
                                        await HeartbeatChannel
                                            .ReceiveClientHeartbeatAcknowledgement(ok.Value.Ack)
                                            .ConfigureAwait(false);
                                    }
                                }
                                
                                break;
                        }
                        break;
                    case TaskResult<PlatformOutboundInstruction>.Error error:
                        if (error.Exception is RpcException { StatusCode: StatusCode.Unavailable } or IOException)
                        {
                            await _connection.ReconnectAsync().ConfigureAwait(false);    
                        }
                        else
                        {
                            _logger.LogCritical("{Exception} remains unhandled and did not cause a reconnect", error.Exception);
                        }
                        
                        foreach (var completion in connected.AwaitedInstructions.Values)
                        {
                            completion.SetException(error.Exception);
                        }
                        connected.AwaitedInstructions.Clear();

                        try
                        {
                            connected.ConsumePlatformOutboundInstructionsCancellationTokenSource.Cancel();
                            await connected.ConsumePlatformOutboundInstructions.ConfigureAwait(false);
                            connected.Call.Dispose();
                            connected.ConsumePlatformOutboundInstructionsCancellationTokenSource.Dispose();
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to clean up call resources");
                        }

                        state = new State.Faulted(connected.EventProcessors);
                        
                        // var due = ScheduleDue.FromException(error.Exception);
                        //
                        // _logger.LogError(error.Exception, "Failed to receive platform outbound instructions. Reconnecting in {Due}ms", due.TotalMilliseconds);
                        //
                        // await _actor.ScheduleAsync(new Message.Reconnect(), due, ct);
                        
                        break;
                }
                
                break;
            case (State.Connected connected, Message.RegisterEventProcessor register):
                if (connected.EventProcessors.Count == 0)
                {
                    await _actor.ScheduleAsync(new Message.SendAllEventProcessorInfo(), _eventProcessorUpdateFrequency, ct);
                }
                
                connected.EventProcessors.RegisterEventProcessor(register.Name, register.Supplier, register.Handler);

                register.Completion.SetResult();
                
                register
                    .Supplier
                    .TellToAsync(
                        _actor,
                        result => new Message.GotEventProcessorInfo(register.Name, InstructionId.New(),  result),
                        ct);
                
                break;
            case (_, Message.RegisterEventProcessor register):
                state.EventProcessors.RegisterEventProcessor(register.Name, register.Supplier, register.Handler);

                register.Completion.SetResult();
                
                break;
            case (_, Message.UnregisterEventProcessor unregister):
                state.EventProcessors.UnregisterEventProcessor(unregister.Name, unregister.Supplier, unregister.Handler);

                unregister.Completion.SetResult();
                
                break;
            case (State.Connected connected, Message.Reconnect):
                try
                {
                    connected.ConsumePlatformOutboundInstructionsCancellationTokenSource.Cancel();
                    await connected.ConsumePlatformOutboundInstructions.ConfigureAwait(false);
                    connected.Call.Dispose();
                    connected.ConsumePlatformOutboundInstructionsCancellationTokenSource.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to clean up call resources");
                }
                
                await _actor.TellAsync(new Message[]
                {
                    new Message.PauseHeartbeats(),
                    new Message.OpenStream()
                }, ct);
                
                state = new State.Reconnecting.StreamClosed(connected.EventProcessors);

                break;
            case (State.Connected, Message.OnConnected):
                await _connection.CheckReadinessAsync();
                
                break;
            case (State.Faulted faulted, Message.Reconnect):
                await _actor.TellAsync(new Message.OpenStream(), ct);
                
                state = new State.Reconnecting.StreamClosed(faulted.EventProcessors);
                
                break;
            default:
                _logger.LogWarning("Skipped {Message} in {State}", message, state);
                
                break;
        }

        return state;
    }

    private async Task ExecuteEventProcessorInstruction(
        EventProcessorName name,
        Func<IEventProcessorInstructionHandler, Task<bool>> execute, 
        InstructionId requestId,
        EventProcessorCollection eventProcessors,
        CancellationToken ct)
    {
        if (eventProcessors.TryGetEventProcessorInstructionHandler(name, out var handler) && handler != null)
        {
            execute(handler)
                .TellToAsync(
                    _actor,
                    () => new Message.SendPlatformInboundInstruction(
                        new PlatformInboundInstruction
                        {
                            Result = new InstructionResult
                            {
                                InstructionId = requestId.ToString(),
                                Success = true
                            }
                        }),
                    exception => new Message.SendPlatformInboundInstruction(
                        new PlatformInboundInstruction
                        {
                            Result = new InstructionResult
                            {
                                InstructionId = requestId.ToString(),
                                Success = false,
                                Error = new ErrorMessage
                                {
                                    ErrorCode = ErrorCategory
                                        .InstructionExecutionError
                                        .ToString(),
                                    Location = _connection
                                        .ClientIdentity
                                        .ClientInstanceId
                                        .ToString(),
                                    Message = exception.Message,
                                    Details =
                                    {
                                        exception.ToString()
                                    }
                                }
                            }
                        }),
                    ct);
        }
        else
        {
            await _actor.TellAsync(new Message.SendPlatformInboundInstruction(
                new PlatformInboundInstruction
                {
                    Result = new InstructionResult
                    {
                        InstructionId = requestId.ToString(),
                        Success = false,
                        Error = new ErrorMessage
                        {
                            ErrorCode = ErrorCategory
                                .InstructionExecutionError
                                .ToString(),
                            Location = _connection
                                .ClientIdentity
                                .ClientInstanceId
                                .ToString(),
                            Message = "Unknown processor"
                        }
                    }
                }), ct);
        }
    }
#pragma warning restore CS4014
#pragma warning restore CA2016
// ReSharper enable MethodSupportsCancellation

    private abstract record Message
    {
        public record Connect : Message;
        
        public record Disconnect : Message;

        public record OpenStream : Message;

        public record StreamOpened(
            TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>
                Result) : Message;

        public record AnnounceClient : Message;

        public record ClientAnnounced(TaskResult Result) : Message;

        public record ResumeHeartbeats : Message;

        public record ResumeEventProcessors : Message;

        public record ReceivePlatformOutboundInstruction(TaskResult<PlatformOutboundInstruction> Result) : Message;

        public record RegisterEventProcessor(EventProcessorName Name, Func<Task<EventProcessorInfo?>> Supplier,
            IEventProcessorInstructionHandler Handler, TaskCompletionSource Completion) : Message;

        public record UnregisterEventProcessor(EventProcessorName Name, Func<Task<EventProcessorInfo?>> Supplier, IEventProcessorInstructionHandler Handler, TaskCompletionSource Completion) : Message;

        public record GotEventProcessorInfo(EventProcessorName Name, InstructionId RequestId, TaskResult<EventProcessorInfo?> Result) : Message;

        public record SendPlatformInboundInstruction(PlatformInboundInstruction Instruction) : Message;
        
        public record SendAwaitablePlatformInboundInstruction(PlatformInboundInstruction Instruction, TaskCompletionSource Completion) : Message;

        public record SendPlatformInboundInstructionFaulted(PlatformInboundInstruction Instruction, Exception Exception) : Message;

        public record SendAllEventProcessorInfo : Message;
        
        public record Reconnect : Message;

        public record PauseHeartbeats : Message;

        public record OnConnected : Message;
    } 
    
    private abstract record State(EventProcessorCollection EventProcessors)
    {
        public record Disconnected(EventProcessorCollection EventProcessors) : State(EventProcessors);

        public record Faulted(EventProcessorCollection EventProcessors) : State(EventProcessors);

        public abstract record Connecting(EventProcessorCollection EventProcessors) : State(EventProcessors)
        {
            public record StreamOpened(AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> Call, EventProcessorCollection EventProcessors) : Connecting(EventProcessors);
            public record StreamClosed(EventProcessorCollection EventProcessors) : Connecting(EventProcessors);
        }

        public abstract record Reconnecting(EventProcessorCollection EventProcessors) : State(EventProcessors)
        {
            public record StreamOpened(AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> Call, EventProcessorCollection EventProcessors) : Reconnecting(EventProcessors);
            public record StreamClosed(EventProcessorCollection EventProcessors) : Reconnecting(EventProcessors);
        }
        
        public record Connected(
            AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction> Call,
            Task ConsumePlatformOutboundInstructions,
            CancellationTokenSource ConsumePlatformOutboundInstructionsCancellationTokenSource,
            Dictionary<InstructionId, TaskCompletionSource> AwaitedInstructions,
            EventProcessorCollection EventProcessors) : State(EventProcessors);
    }
    

    internal async ValueTask Connect()
    {
        await _actor.TellAsync(
            new Message.Connect()
        ).ConfigureAwait(false);
    }

    internal async ValueTask Reconnect()
    {
        await _actor.TellAsync(
            new Message.Reconnect()
        ).ConfigureAwait(false);
    }

    internal async ValueTask Disconnect()
    {
        await _actor.TellAsync(
            new Message.Disconnect()
        ).ConfigureAwait(false);
    }

    public async Task<IEventProcessorRegistration> RegisterEventProcessorAsync(
        EventProcessorName name,
        Func<Task<EventProcessorInfo?>> supplier,
        IEventProcessorInstructionHandler handler)
    {
        if (supplier == null) throw new ArgumentNullException(nameof(supplier));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var registerCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _actor.TellAsync(new Message.RegisterEventProcessor(
            name,
            supplier,
            handler,
            registerCompletionSource), CancellationToken.None).ConfigureAwait(false);
        return new EventProcessorRegistration(registerCompletionSource.Task, async () =>
        {
            var unsubscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor
                .TellAsync(new Message.UnregisterEventProcessor(name, supplier, handler, unsubscribeCompletionSource))
                .ConfigureAwait(false);
            await unsubscribeCompletionSource.Task.ConfigureAwait(false);
        });
    }

    public async Task SendInstructionAsync(PlatformInboundInstruction instruction)
    {
        if (instruction == null) throw new ArgumentNullException(nameof(instruction));
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _actor.TellAsync(new Message.SendAwaitablePlatformInboundInstruction(instruction, completionSource));
        await completionSource.Task;
    }

    public Task EnableHeartbeatAsync(TimeSpan interval, TimeSpan timeout)
    {
        return HeartbeatChannel.Enable(interval, timeout);
    }

    public Task DisableHeartbeatAsync()
    {
        return HeartbeatChannel.Disable();
    }

    public bool IsConnected => _actor.State is State.Connected;

    public async ValueTask DisposeAsync()
    {
        await HeartbeatChannel.DisposeAsync().ConfigureAwait(false);
        await _actor.DisposeAsync().ConfigureAwait(false);
    }
}