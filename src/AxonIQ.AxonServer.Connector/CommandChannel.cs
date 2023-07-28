using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class CommandChannel : ICommandChannel, IAsyncDisposable
{
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromSeconds(15);
    
    private readonly IOwnerAxonServerConnection _connection;
    private readonly AxonActor<Message, State> _actor;
    private readonly TimeSpan _purgeInterval;
    private readonly ILogger<CommandChannel> _logger;

    public CommandChannel(
        IOwnerAxonServerConnection connection,
        IScheduler scheduler,
        PermitCount permits,
        PermitCount permitsBatch,
        ReconnectOptions reconnectOptions,
        ILoggerFactory loggerFactory)
        : this(connection, scheduler, permits, permitsBatch, reconnectOptions, PurgeInterval, loggerFactory)
    {
    }

    public CommandChannel(
        IOwnerAxonServerConnection connection,
        IScheduler scheduler,
        PermitCount permits,
        PermitCount permitsBatch,
        ReconnectOptions reconnectOptions,
        TimeSpan purgeInterval,
        ILoggerFactory loggerFactory)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        if (reconnectOptions == null) throw new ArgumentNullException(nameof(reconnectOptions));
        
        _connection = connection;
        _purgeInterval = purgeInterval;

        ReconnectOptions = reconnectOptions;
        Service = new CommandService.CommandServiceClient(connection.CallInvoker);
        
        _logger = loggerFactory.CreateLogger<CommandChannel>();
        _actor = new AxonActor<Message, State>(
            Receive,
            new State.Disconnected(
                new CommandHandlerCollection(connection.ClientIdentity, scheduler.Clock),
                new FlowController(permits, permitsBatch)),
            scheduler,
            _logger);
    }

// ReSharper disable MethodSupportsCancellation
#pragma warning disable CS4014
#pragma warning disable CA2016
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected disconnected, Message.Connect):
                await _actor.TellAsync(new Message.OpenStream(), ct).ConfigureAwait(false);
                state = new State.Connecting(disconnected.CommandHandlers, disconnected.Flow);
                break;
            case (State.Connecting, Message.OpenStream):
                await _actor.TellAsync(
                    () => Service.OpenStream(cancellationToken: ct),
                    result => new Message.StreamOpened(result),
                    ct);
                break;
            case (State.Connecting connecting, Message.StreamOpened opened):
                switch (opened.Result)
                {
                    case TaskResult<AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound>>.Ok ok:
                        _logger.LogInformation(
                            "Opened command stream for context '{Context}'",
                            _connection.Context);
                        
                        var consumerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var consumer =
                            ok
                                .Value
                                .ResponseStream
                                .TellToAsync(
                                    _actor,
                                    result => new Message.ReceiveCommandProviderInbound(result),
                                    _logger,
                                    consumerCancellationTokenSource.Token);
                        
                        connecting.Flow.Reset();

                        await _actor.TellAsync(
                            new Message[]
                            {
                                new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                                {
                                    FlowControl = new FlowControl
                                    {
                                        ClientId = _connection.ClientIdentity.ClientInstanceId.ToString(),
                                        Permits = connecting.Flow.Initial.ToInt64()
                                    }
                                }),
                                new Message.OnConnected()
                            }, ct).ConfigureAwait(false);

                        await _actor.TellAsync(
                            connecting
                                .CommandHandlers
                                .RegisteredCommands
                                .Select<RegisteredCommandId, Message>(id =>
                                    new Message.SendCommandProviderOutbound(
                                        connecting
                                            .CommandHandlers
                                            .BeginSubscribeToCommandInstruction(id)))
                                .ToArray(), ct).ConfigureAwait(false);
                        
                        if (connecting.CommandHandlers.RegisteredCommands.Count != 0)
                        {
                            await _actor.ScheduleAsync(new Message.PurgeOverdueInstructions(), _purgeInterval, ct).ConfigureAwait(false);
                        }
                        
                        state = new State.Connected(
                            ok.Value,
                            consumer,
                            consumerCancellationTokenSource,
                            connecting.CommandHandlers, 
                            connecting.Flow);
                        
                        break;
                    case TaskResult<AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound>>.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open command stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct).ConfigureAwait(false);
                        
                        break;
                }
                
                break;
            case (State.Connected connected, Message.ReceiveCommandProviderInbound receive):
                switch (receive.Result)
                {
                    case TaskResult<CommandProviderInbound>.Ok ok:
                        if (InstructionId.TryParse(ok.Value.InstructionId, out var instructionId))
                        {
                            await _actor.TellAsync(new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                            {
                                Ack = new InstructionAck
                                {
                                    InstructionId = instructionId.ToString(),
                                    Success = true
                                }
                            }), ct);
                        }
                        
                        switch (ok.Value.RequestCase)
                        {
                            case CommandProviderInbound.RequestOneofCase.Command:
                                var requestId = InstructionId.Parse(ok.Value.Command.MessageIdentifier);
                                var name = new CommandName(ok.Value.Command.Name);
                                if (connected.CommandHandlers.TryGetCommandHandler(name, out var handler))
                                {
                                    handler?
                                        .Invoke(ok.Value.Command, ct)
                                        .TellToAsync(
                                            _actor,
                                            result => new Message.CommandHandled(requestId, result),
                                            ct);
                                }
                                else
                                {
                                    await _actor.TellAsync(new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                                    {
                                        CommandResponse = new CommandResponse
                                        {
                                            RequestIdentifier =
                                                ok.Value.Command.MessageIdentifier,
                                            ErrorCode = ErrorCategory.NoHandlerForCommand.ToString(),
                                            ErrorMessage = new ErrorMessage
                                                { Message = "No Handler for command" }
                                        }
                                    }), ct);
                                }
                                break;
                            case CommandProviderInbound.RequestOneofCase.Ack:
                                if (connected
                                    .CommandHandlers
                                    .TryCompleteSubscribeToCommandInstruction(ok.Value.Ack))
                                {
                                    //???
                                }

                                if (connected
                                    .CommandHandlers
                                    .TryCompleteUnsubscribeFromCommandInstruction(ok.Value.Ack))
                                {
                                    
                                }
                                break;
                        }
                        
                        if (connected.Flow.Increment())
                        {
                            await _actor.TellAsync(new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                            {
                                FlowControl = new FlowControl
                                {
                                    ClientId = _connection.ClientIdentity.ClientInstanceId.ToString(),
                                    Permits = connected.Flow.Threshold.ToInt64()
                                }
                            }), ct);
                        }

                        break;
                    case TaskResult<CommandProviderInbound>.Error:
                        try
                        {
                            connected.ConsumeCommandProviderInboundInstructionsCancellationTokenSource.Cancel();
                            await connected.ConsumeCommandProviderInboundInstructions.ConfigureAwait(false);
                            connected.Call.Dispose();
                            connected.ConsumeCommandProviderInboundInstructionsCancellationTokenSource.Dispose();
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to clean up call resources");
                        }

                        state = new State.Faulted(connected.CommandHandlers, connected.Flow);
                        break;
                }
                break;
            case (State.Connected, Message.CommandHandled handled):
                switch (handled.Result)
                {
                    case TaskResult<CommandResponse>.Ok ok:
                        await _actor.TellAsync(new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                        {
                            CommandResponse = new CommandResponse(ok.Value)
                            {
                                RequestIdentifier = handled.RequestId.ToString()
                            }
                        }), ct).ConfigureAwait(false);
                        break;
                    case TaskResult<CommandResponse>.Error error:
                        await _actor.TellAsync(new Message.SendCommandProviderOutbound(new CommandProviderOutbound
                        { 
                            CommandResponse = new CommandResponse
                            {
                                RequestIdentifier = handled.RequestId.ToString(),
                                ErrorCode = ErrorCategory.CommandExecutionError.ToString(),
                                ErrorMessage = new ErrorMessage
                                {
                                    Details =
                                    {
                                        error.Exception.ToString()
                                    },
                                    Message = error.Exception.Message
                                }
                            }
                        }), ct).ConfigureAwait(false);
                        break;
                }
                break;
            case (State.Connected connected, Message.SendCommandProviderOutbound send):
                await connected
                    .Call
                    .RequestStream
                    .WriteAsync(send.Instruction)
                    .TellToAsync(
                        _actor,
                        exception => new Message.SendCommandProviderOutboundFaulted(send.Instruction, exception),
                        ct)
                    .ConfigureAwait(false);
                break;
            case (State.Connected, Message.SendCommandProviderOutboundFaulted faulted):
                _logger.LogWarning("An error occurred while sending a command provider outbound instruction: {Exception}", faulted.Exception.ToString());
                break;
            case(State.Connected, Message.OnConnected):
                await _connection.CheckReadinessAsync().ConfigureAwait(false);
                break;
            case(State.Connected connected, Message.RegisterCommandHandler register):
                if (connected.CommandHandlers.RegisteredCommands.Count == 0)
                {
                    await _actor
                        .ScheduleAsync(new Message.PurgeOverdueInstructions(), _purgeInterval, ct)
                        .ConfigureAwait(false);
                }
                
                state
                    .CommandHandlers
                    .RegisterCommandHandler(
                        register.Id,
                        register.Name,
                        register.LoadFactor,
                        register.Handler);
                
                state
                    .CommandHandlers
                    .RegisterCommandSubscribeCompletionSource(
                        register.Id,
                        register.CompletionSource);

                await _actor
                    .TellAsync(
                        new Message.SendCommandProviderOutbound(
                            state
                                .CommandHandlers
                                .BeginSubscribeToCommandInstruction(register.Id)
                        )
                    , ct)
                    .ConfigureAwait(false);

                break;
            case(State.Disconnected, Message.RegisterCommandHandler register):
                state
                    .CommandHandlers
                    .RegisterCommandHandler(
                        register.Id,
                        register.Name,
                        register.LoadFactor,
                        register.Handler);
                
                state
                    .CommandHandlers
                    .RegisterCommandSubscribeCompletionSource(
                        register.Id,
                        register.CompletionSource);
                
                // instructions will be sent once connected
                await _actor.TellAsync(new Message.Connect()).ConfigureAwait(false);
                break;
            case(State.Connecting, Message.RegisterCommandHandler register):
                state
                    .CommandHandlers
                    .RegisterCommandHandler(
                        register.Id,
                        register.Name,
                        register.LoadFactor,
                        register.Handler);
                
                state
                    .CommandHandlers
                    .RegisterCommandSubscribeCompletionSource(
                        register.Id,
                        register.CompletionSource);
                // instructions will be sent once connected
                break;
            case(State.Faulted, Message.RegisterCommandHandler register):
                register.CompletionSource.TrySetException(
                    new AxonServerException(
                        _connection.ClientIdentity,
                        ErrorCategory.InstructionAckError,
                        "Unable to send instruction: no connection to AxonServer"));
                break;
            case(State.Connected, Message.UnregisterCommandHandler unregister):
                state
                    .CommandHandlers
                    .RegisterCommandUnsubscribeCompletionSource(
                        unregister.Id,
                        unregister.CompletionSource);
                
                await _actor
                    .TellAsync(
                        new Message.SendCommandProviderOutbound(
                            state
                                .CommandHandlers
                                .BeginUnsubscribeFromCommandInstruction(unregister.Id)
                        )
                        , ct)
                    .ConfigureAwait(false);
                break;
            case(_, Message.UnregisterCommandHandler unregister):
                state
                    .CommandHandlers
                    .UnregisterCommandHandler(unregister.Id);

                unregister.CompletionSource.TrySetException(
                    new AxonServerException(
                        _connection.ClientIdentity,
                        ErrorCategory.InstructionAckError,
                        "Unable to send instruction: no connection to AxonServer"));
                break;
            case(State.Connected connected, Message.PurgeOverdueInstructions purge):
                state.CommandHandlers.Purge(_purgeInterval);

                if (connected.CommandHandlers.RegisteredCommands.Count != 0)
                {
                    await _actor.ScheduleAsync(purge, _purgeInterval, ct).ConfigureAwait(false);
                }
                break;
            case(State.Connecting, Message.PurgeOverdueInstructions):
            case(State.Disconnected, Message.PurgeOverdueInstructions):
            case(State.Faulted, Message.PurgeOverdueInstructions):
                state.CommandHandlers.Purge(_purgeInterval);
                break;
            case (State.Faulted faulted, Message.Reconnect):
                await _actor.TellAsync(new Message.OpenStream(), ct).ConfigureAwait(false);
                state = new State.Connecting(faulted.CommandHandlers, faulted.Flow);
                break;
            default:
                _logger.LogWarning("Skipped {Message} in {State}", message, state);
                break;
        }
        
        return state;
    }
#pragma warning restore CS4014
#pragma warning restore CA2016
// ReSharper enable MethodSupportsCancellation

    public ReconnectOptions ReconnectOptions { get; }
    public CommandService.CommandServiceClient Service { get; }

    private abstract record Message
    {
        public record Connect : Message;

        public record OpenStream : Message;

        public record StreamOpened(
            TaskResult<AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound>> Result) : Message;

        public record ReceiveCommandProviderInbound(
            TaskResult<CommandProviderInbound> Result) : Message;

        public record OnConnected : Message;

        public record SendCommandProviderOutbound(CommandProviderOutbound Instruction) : Message;
        
        public record SendCommandProviderOutboundFaulted
            (CommandProviderOutbound Instruction, Exception Exception) : Message;

        public record CommandHandled(InstructionId RequestId, TaskResult<CommandResponse> Result) : Message;
        
        public record RegisterCommandHandler(
            RegisteredCommandId Id,
            CommandName Name,
            LoadFactor LoadFactor,
            Func<Command, CancellationToken, Task<CommandResponse>> Handler,
            TaskCompletionSource CompletionSource) : Message;

        public record UnregisterCommandHandler(
            RegisteredCommandId Id,
            TaskCompletionSource CompletionSource) : Message;

        public record PurgeOverdueInstructions : Message;

        public record Reconnect : Message;
    }

    private abstract record State(CommandHandlerCollection CommandHandlers, FlowController Flow)
    {
        public record Disconnected(CommandHandlerCollection CommandHandlers, FlowController Flow) 
            : State(CommandHandlers, Flow);

        public record Connecting(CommandHandlerCollection CommandHandlers, FlowController Flow)
            : State(CommandHandlers, Flow);
        
        public record Connected(
                AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound> Call,
                Task ConsumeCommandProviderInboundInstructions,
                CancellationTokenSource ConsumeCommandProviderInboundInstructionsCancellationTokenSource,
                CommandHandlerCollection CommandHandlers, FlowController Flow)
            : State(CommandHandlers, Flow);

        public record Faulted(CommandHandlerCollection CommandHandlers, FlowController Flow) 
            : State(CommandHandlers, Flow);
    }
    
    public bool IsConnected => 
        !_actor.State.CommandHandlers.HasRegisteredCommands 
        || (_actor.State.CommandHandlers.HasRegisteredCommands && _actor.State is State.Connected);

    internal async ValueTask Reconnect()
    {
        await _actor.TellAsync(new Message.Reconnect()).ConfigureAwait(false);
    }
    
    public async Task<ICommandHandlerRegistration> RegisterCommandHandlerAsync(
        Func<Command, CancellationToken, Task<CommandResponse>> handler,
        LoadFactor loadFactor,
        params CommandName[] commandNames)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (commandNames == null) throw new ArgumentNullException(nameof(commandNames));
        if (commandNames.Length == 0)
            throw new ArgumentException("The command names requires at least one command name to be specified",
                nameof(commandNames));

        var registeredCommands = commandNames
            .Select(commandName => new { Id = RegisteredCommandId.New(), Name = commandName })
            .ToArray();

        var subscribeCompletions = new List<Task>();
        foreach (var registeredCommand in registeredCommands)
        {
            var subscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor
                .TellAsync(new Message.RegisterCommandHandler(
                    registeredCommand.Id,
                    registeredCommand.Name,
                    loadFactor,
                    handler,
                    subscribeCompletionSource))
                .ConfigureAwait(false);
            subscribeCompletions.Add(subscribeCompletionSource.Task);
        }
        
        return new CommandHandlerRegistration(Task.WhenAll(subscribeCompletions), async () =>
        {
            var unsubscribeCompletions = new List<Task>();
            foreach (var registeredCommand in registeredCommands)
            {
                var unsubscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await _actor
                    .TellAsync(new Message.UnregisterCommandHandler(
                        registeredCommand.Id,
                        unsubscribeCompletionSource))
                    .ConfigureAwait(false);
                unsubscribeCompletions.Add(unsubscribeCompletionSource.Task);
            }

            await Task.WhenAll(unsubscribeCompletions).ConfigureAwait(false);
        });
    }

    public async Task<CommandResponse> SendCommandAsync(Command command, CancellationToken ct)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        
        using var activity = 
            Telemetry.Source.StartActivity("CommandChannel.SendCommandAsync")
                .SetClientIdTag(_connection.ClientIdentity.ClientInstanceId)
                .SetComponentNameTag(_connection.ClientIdentity.ComponentName)
                .SetContextTag(_connection.Context);
        
        var request = new Command(command)
        {
            ClientId = _connection.ClientIdentity.ClientInstanceId.ToString(),
            ComponentName = _connection.ClientIdentity.ComponentName.ToString()
        };
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = InstructionId.New().ToString();
        }

        if (request.ProcessingInstructions.All(instruction => instruction.Key != ProcessingKey.RoutingKey))
        {
            request.ProcessingInstructions.Add(new ProcessingInstruction
            {
                Key = ProcessingKey.RoutingKey,
                Value = new MetaDataValue
                {
                    TextValue = request.MessageIdentifier
                }
            });
        }
        
        activity
            .SetMessageIdTag(request.MessageIdentifier)
            .SetCommandNameTag(request.Name);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Dispatching the command {Command} with message identifier {MessageIdentifier}",
                request.Name, request.MessageIdentifier);
        }
        
        try
        {
            return await Service.DispatchAsync(request, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                _connection.ClientIdentity,
                ErrorCategory.CommandDispatchError,
                "An error occurred while attempting to dispatch a command",
                exception);
        }
    }

    public ValueTask DisposeAsync() => _actor.DisposeAsync();
}