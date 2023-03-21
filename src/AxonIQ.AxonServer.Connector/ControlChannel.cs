using System.Diagnostics.CodeAnalysis;
using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public class ControlChannel : IControlChannel, IAsyncDisposable
{
    private readonly AxonActor<Message, State> _actor;
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
            new State.Disconnected(new EventProcessorCollection()),
            scheduler,
            _logger
        );
        HeartbeatChannel = new HeartbeatChannel(
            instruction => _actor.TellAsync(new Message.SendPlatformInboundInstruction(instruction), CancellationToken.None),
            () => RequestReconnect(),
            scheduler,
            loggerFactory.CreateLogger<HeartbeatChannel>()
        );
    }

    public ClientIdentity ClientIdentity { get; }

    public CallInvoker CallInvoker { get; }

    public RequestReconnect RequestReconnect { get; }

    public PlatformService.PlatformServiceClient Service { get; }

    public HeartbeatChannel HeartbeatChannel { get; }

#pragma warning disable CS4014
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected disconnected, Message.Connect):
                await _actor.TellAsync(new Message.OpenStream(), ct);

                state = new State.Connecting.StreamClosed(disconnected.EventProcessors);
                break;
            case (State.Disconnected disconnected, Message.Reconnect):
                await _actor.TellAsync(new Message[]
                {
                    new Message.OpenStream(), 
                    new Message.PauseHeartbeats()
                }, ct);

                state = new State.Reconnecting.StreamClosed(disconnected.EventProcessors);
                break;
            case (State.Reconnecting.StreamClosed, Message.OpenStream):
                _actor.TellAsync(
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
                        var due = ReconnectAfterPolicy.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);
                        
                        break;
                }

                break;
            case (State.Reconnecting.StreamOpened opened, Message.AnnounceClient):
                opened.Call.RequestStream.WriteAsync(new PlatformInboundInstruction
                {
                    Register = ClientIdentity.ToClientIdentification()
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

                        state = new State.Connected(opened.Call, consumer, consumerTokenSource, opened.EventProcessors);
                        
                        break;
                    case TaskResult.Error error:
                        var due = ReconnectAfterPolicy.FromException(error.Exception);
                        
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
                _actor.TellAsync(
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
                        var due = ReconnectAfterPolicy.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct);
                        
                        break;
                }

                break;
            case (State.Connecting.StreamOpened opened, Message.AnnounceClient):
                opened.Call.RequestStream.WriteAsync(new PlatformInboundInstruction
                {
                    Register = ClientIdentity.ToClientIdentification()
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

                        state = new State.Connected(opened.Call, consumer, consumerTokenSource, opened.EventProcessors);
                        
                        break;
                    case TaskResult.Error error:
                        var due = ReconnectAfterPolicy.FromException(error.Exception);
                        
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
                        supplier()
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
                                _context.ToString());
                        }
                        break;
                    case TaskResult<EventProcessorInfo?>.Error error:
                        _logger.LogError(
                            error.Exception,
                            "Not sending processor info {Name} for context '{Context}'",
                            gotten.Name.ToString(),
                            _context.ToString());
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
                var due = ReconnectAfterPolicy.FromException(faulted.Exception);

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
                    send.Completion.SetResult();
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unable to send instruction {Instruction}",
                        send.Instruction);
                    
                    send.Completion.SetException(
                        new AxonServerException(
                            ClientIdentity,
                            ErrorCategory.InstructionAckError,
                            "Unable to send instruction: AxonServer unavailable"));
                }
                break;
            case (_, Message.SendAwaitablePlatformInboundInstruction send):
                send.Completion.SetException(
                    new AxonServerException(
                        ClientIdentity,
                        ErrorCategory.InstructionAckError,
                        "Unable to send instruction: no connection to AxonServer"));
                break;
            case (State.Connected connected, Message.ReceivePlatformOutboundInstruction received):
                switch (received.Result)
                {
                    case TaskResult<PlatformOutboundInstruction>.Ok ok:
                        switch (ok.Value.RequestCase)
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
                                    new EventProcessorName(ok.Value.PauseEventProcessor.ProcessorName),
                                    async handler =>
                                    {
                                        await handler.Pause();
                                        return true;
                                    },
                                    new InstructionId(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.StartEventProcessor:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.StartEventProcessor.ProcessorName),
                                    async handler =>
                                    {
                                        await handler.Start();
                                        return true;
                                    },
                                    new InstructionId(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.ReleaseSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.ReleaseSegment.ProcessorName),
                                    handler => handler.ReleaseSegment(new SegmentId(ok.Value.ReleaseSegment.SegmentIdentifier)),
                                    new InstructionId(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.RequestEventProcessorInfo:
                                var name = new EventProcessorName(ok.Value.RequestEventProcessorInfo.ProcessorName);
                                if (connected.EventProcessors.TryGetEventProcessorInfoSupplier(name, out var supplier))
                                {
                                    supplier?
                                        .Invoke()
                                        .TellToAsync(
                                            _actor,
                                            result => new Message.GotEventProcessorInfo(
                                                name,
                                                new InstructionId(ok.Value.InstructionId),
                                                result),
                                            ct
                                        );
                                }
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.SplitEventProcessorSegment.ProcessorName),
                                    handler => handler.SplitSegment(new SegmentId(ok.Value.SplitEventProcessorSegment.SegmentIdentifier)),
                                    new InstructionId(ok.Value.InstructionId),
                                    connected.EventProcessors,
                                    ct
                                );
                                break;
                            case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
                                await ExecuteEventProcessorInstruction(
                                    new EventProcessorName(ok.Value.MergeEventProcessorSegment.ProcessorName),
                                    handler => handler.MergeSegment(new SegmentId(ok.Value.MergeEventProcessorSegment.SegmentIdentifier)),
                                    new InstructionId(ok.Value.InstructionId),
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
                                await HeartbeatChannel.ReceiveClientHeartbeatAcknowledgement(ok.Value.Ack).ConfigureAwait(false);
                                
                                break;
                        }
                        break;
                    case TaskResult<PlatformOutboundInstruction>.Error error:
                        var due = ReconnectAfterPolicy.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to receive platform outbound instructions. Reconnecting in {Due}ms", due.TotalMilliseconds);

                        await _actor.ScheduleAsync(new Message.Reconnect(), due, ct);
                        
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
                    .Supplier()
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
                    _logger.LogError(exception, "Failed to clean up platform instructions streaming call resources");
                }

                await _actor.TellAsync(new Message[]
                {
                    new Message.PauseHeartbeats(),
                    new Message.OpenStream()
                }, ct);

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
                            Location = ClientIdentity
                                .ClientInstanceId
                                .ToString(),
                            Message = "Unknown processor"
                        }
                    }
                }), ct);
        }
    }
#pragma warning restore CS4014

    private abstract record Message
    {
        public record Connect : Message;
        
        public record Disconnect : Message;

        public record OpenStream : Message;

        public record StreamOpened(
            TaskResult<AsyncDuplexStreamingCall<PlatformInboundInstruction, PlatformOutboundInstruction>>
                Result) : Message;

        public record AnnounceClient() : Message;

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

        public record PlatformInboundInstructionSent(PlatformInboundInstruction Instruction, TaskResult Result) : Message;
        
        public record SendPlatformInboundInstructionFaulted(PlatformInboundInstruction Instruction, Exception Exception) : Message;

        public record SendAllEventProcessorInfo : Message;
        public record SendEventProcessorInfo(EventProcessorName Name, TaskResult<EventProcessorInfo> Result) : Message;
        public record Reconnect : Message;

        public record PauseHeartbeats : Message;
    } 
    private abstract record State(EventProcessorCollection EventProcessors)
    {
        public record Disconnected(EventProcessorCollection EventProcessors) : State(EventProcessors);

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

    public async Task<IEventProcessorRegistration> RegisterEventProcessor(
        EventProcessorName name,
        Func<Task<EventProcessorInfo>> infoSupplier,
        IEventProcessorInstructionHandler handler)
    {
        if (infoSupplier == null) throw new ArgumentNullException(nameof(infoSupplier));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var registerCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _actor.TellAsync(new Message.RegisterEventProcessor(
            name,
            infoSupplier,
            handler,
            registerCompletionSource), CancellationToken.None).ConfigureAwait(false);
        return new EventProcessorRegistration(registerCompletionSource.Task, async () =>
        {
            var unsubscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor
                .TellAsync(new Message.UnregisterEventProcessor(name, infoSupplier, handler, unsubscribeCompletionSource))
                .ConfigureAwait(false);
            await unsubscribeCompletionSource.Task.ConfigureAwait(false);
        });
    }

    public async Task SendInstruction(PlatformInboundInstruction instruction)
    {
        if (instruction == null) throw new ArgumentNullException(nameof(instruction));
        if (!string.IsNullOrEmpty(instruction.InstructionId))
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor.TellAsync(new Message.SendAwaitablePlatformInboundInstruction(instruction, completionSource));
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
}