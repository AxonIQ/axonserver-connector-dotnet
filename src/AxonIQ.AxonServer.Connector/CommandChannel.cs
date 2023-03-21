using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public class CommandChannel : ICommandChannel, IAsyncDisposable
{
    private readonly AxonActor<Message, State> _actor;
    private readonly ILogger<CommandChannel> _logger;

    public CommandChannel(
        ClientIdentity clientIdentity,
        Context context,
        IScheduler scheduler,
        CallInvoker callInvoker,
        PermitCount permits,
        PermitCount permitsBatch,
        BackoffPolicyOptions connectBackoffPolicyOptions,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));

        ClientIdentity = clientIdentity;
        Context = context;
        Service = new CommandService.CommandServiceClient(callInvoker);
        _logger = loggerFactory.CreateLogger<CommandChannel>();
        _actor = new AxonActor<Message, State>(
            Receive,
            new State.Disconnected(
                new CommandRegistrations(clientIdentity, scheduler.Clock),
                new FlowController(permits, permitsBatch),
                new TaskRunCache(),
                new BackoffPolicy(connectBackoffPolicyOptions)),
            scheduler,
            _logger);
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<CommandProviderInbound> reader, CancellationToken ct)
    {
        try
        {
            try
            {
                await foreach (var response in reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    await _actor.TellAsync(new Message.ReceiveCommandProviderInbound(response), ct)
                        .ConfigureAwait(false);
                }
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
            {
                await _actor.TellAsync(new Message.Reconnect(), ct).ConfigureAwait(false);
                _logger.LogError(
                    exception,
                    "The command channel instruction stream is no longer being read because of an unexpected exception. Reconnection has been scheduled");
            }
            catch (RpcException exception) when (exception.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogWarning(
                    exception,
                    "The command channel instruction stream is no longer being read because of the call got cancelled. Reconnection has NOT been scheduled");
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception,
                "The command channel instruction stream is no longer being read because an object got disposed");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The command channel instruction stream is no longer being read because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "The command channel instruction stream is no longer being read because of an unexpected exception. Reconnection has NOT been scheduled");
        }
    }

    private async Task<State> EnsureConnected(State state, CancellationToken ct)
    {
        switch (state)
        {
            case State.Disconnected disconnected:
                try
                {
                    var stream = Service.OpenStream(cancellationToken: ct);
                    if (stream != null)
                    {
                        _logger.LogInformation(
                            "Opened command stream for context '{Context}'",
                            Context);

                        foreach (var entry in disconnected.CommandRegistrations.AllSubscriptions)
                        {
                            var handler =
                                disconnected.CommandRegistrations.AllCommandHandlers[entry.Value.CommandHandlerRegistrationId];

                            var instructionId =
                                disconnected.CommandRegistrations.SubscribeToCommand(handler.CommandHandlerRegistrationId,
                                    entry.Key, entry.Value.Command);
                            
                            var request = new CommandProviderOutbound
                            {
                                InstructionId = instructionId.ToString(),
                                Subscribe = new CommandSubscription
                                {
                                    MessageId = instructionId.ToString(),
                                    Command = entry.Value.Command.ToString(),
                                    LoadFactor = handler.LoadFactor.ToInt32(),
                                    ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                    ComponentName = ClientIdentity.ComponentName.ToString()
                                }
                            };

                            await stream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                        }

                        await stream.RequestStream.WriteAsync(new CommandProviderOutbound
                        {
                            FlowControl = new FlowControl
                            {
                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                Permits = disconnected.Flow.Initial.ToInt64()
                            }
                        }).ConfigureAwait(false);

                        disconnected.Flow.Reset();
                        disconnected.ConnectBackoffPolicy.Reset();
                        
                        state = new State.Connected(
                            stream,
                            ConsumeResponseStream(stream.ResponseStream, ct),
                            disconnected.CommandRegistrations,
                            disconnected.Flow,
                            disconnected.CommandTasks,
                            disconnected.ConnectBackoffPolicy
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not open command stream for context '{Context}'",
                            Context);
                    }
                }
                catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                {
                    _logger.LogWarning(
                        "Could not open command stream for context '{Context}': no connection to AxonServer",
                        Context.ToString());
                }

                break;
            case State.Connected:
                _logger.LogDebug("CommandChannel for context '{Context}' is already connected",
                    Context.ToString());
                break;
        }

        return state;
    }

    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (message)
        {
            case Message.Connect:
                state = await EnsureConnected(state, ct).ConfigureAwait(false);
                
                if (state is State.Disconnected)
                {
                    await _actor.ScheduleAsync(new Message.Connect(), state.ConnectBackoffPolicy.Next(), ct).ConfigureAwait(false);
                }
                else
                {
                    state.ConnectBackoffPolicy.Reset();
                }

                break;
            case Message.RegisterCommandHandler register:
                state = await EnsureConnected(state, ct).ConfigureAwait(false);
                
                switch (state)
                {
                    case State.Connected connected:
                        connected.CommandRegistrations.RegisterCommandHandler(
                            register.RegistrationId,
                            register.CompletionSource,
                            register.LoadFactor,
                            register.Handler);

                        foreach (var (subscriptionId, command) in register.RegisteredCommands)
                        {
                            _logger.LogInformation(
                                "Registered handler for command '{CommandName}' in context '{Context}'",
                                command.ToString(), Context.ToString());

                            var instructionId = connected.CommandRegistrations.SubscribeToCommand(register.RegistrationId,
                                subscriptionId, command);
                            var request = new CommandProviderOutbound
                            {
                                InstructionId = instructionId.ToString(),
                                Subscribe = new CommandSubscription
                                {
                                    MessageId = instructionId.ToString(),
                                    Command = command.ToString(),
                                    LoadFactor = register.LoadFactor.ToInt32(),
                                    ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                    ComponentName = ClientIdentity.ComponentName.ToString()
                                }
                            };
                            await connected.Stream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                        }

                        break;
                    case State.Disconnected:
                        if (!register.CompletionSource.Fault(
                                new AxonServerException(
                                    ClientIdentity,
                                    ErrorCategory.Other,
                                    "Unable to subscribe commands and handler: no connection to AxonServer")))
                        {
                            _logger.LogWarning(
                                "Could not fault the subscribe completion source of command handler '{CommandHandlerId}'",
                                register.RegistrationId.ToString());
                        }

                        break;
                }

                break;
            case Message.UnregisterCommandHandler unregister:
                switch (state)
                {
                    case State.Connected connected:
                        connected.CommandRegistrations.UnregisterCommandHandler(
                            unregister.RegistrationId,
                            unregister.CompletionSource);
                        
                        foreach (var (subscriptionId, command) in unregister.RegisteredCommands)
                        {
                            var instructionId = connected.CommandRegistrations.UnsubscribeFromCommand(subscriptionId);
                            if (instructionId.HasValue)
                            {
                                _logger.LogInformation(
                                    "Unregistered handler for command '{CommandName}' in context '{Context}'",
                                    command.ToString(), Context.ToString());

                                var request = new CommandProviderOutbound
                                {
                                    InstructionId = instructionId.ToString(),
                                    Unsubscribe = new CommandSubscription
                                    {
                                        MessageId = instructionId.ToString(),
                                        Command = command.ToString(),
                                        ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                        ComponentName = ClientIdentity.ComponentName.ToString()
                                    }
                                };
                                await connected.Stream.RequestStream.WriteAsync(request).ConfigureAwait(false);
                            }
                        }

                        break;
                    case State.Disconnected:
                        if (!unregister.CompletionSource.Fault(
                                new AxonServerException(
                                    ClientIdentity,
                                    ErrorCategory.Other,
                                    "Unable to unsubscribe commands and handler: no connection to AxonServer")))
                        {
                            _logger.LogWarning(
                                "Could not fault the unsubscribe completion source of command handler '{CommandHandlerId}'",
                                unregister.RegistrationId.ToString());
                        }

                        break;
                }

                break;
            case Message.Reconnect:
                switch (state)
                {
                    case State.Connected connected:
                        await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);
                        connected.Stream.Dispose();
                        connected.Flow.Reset();

                        state = new State.Disconnected(
                            connected.CommandRegistrations, 
                            connected.Flow,
                            connected.CommandTasks,
                            connected.ConnectBackoffPolicy);
                        break;
                }

                state = await EnsureConnected(state, ct);

                if (state is State.Disconnected)
                {
                    await _actor.ScheduleAsync(new Message.Reconnect(), state.ConnectBackoffPolicy.Next(), ct).ConfigureAwait(false);
                }
                else
                {
                    state.ConnectBackoffPolicy.Reset();
                }
                    
                break;
            case Message.Disconnect:
                switch (state)
                {
                    case State.Connected connected:
                        await connected.ConsumeResponseStreamLoop.ConfigureAwait(false);
                        connected.Stream.Dispose();
                        connected.Flow.Reset();

                        state = new State.Disconnected(
                            connected.CommandRegistrations, 
                            connected.Flow,
                            connected.CommandTasks,
                            connected.ConnectBackoffPolicy);
                        break;
                }

                break;
            case Message.ReceiveCommandProviderInbound receive:
                switch (state)
                {
                    case State.Connected connected:
                        switch (receive.Message.RequestCase)
                        {
                            case CommandProviderInbound.RequestOneofCase.None:
                                break;
                            case CommandProviderInbound.RequestOneofCase.Ack:
                                connected.CommandRegistrations.Acknowledge(receive.Message.Ack);
                                
                                if (connected.Flow.Increment())
                                {
                                    await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                    {
                                        FlowControl = new FlowControl
                                        {
                                            ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                            Permits = connected.Flow.Threshold.ToInt64()
                                        }
                                    }).ConfigureAwait(false);
                                }
                                break;
                            case CommandProviderInbound.RequestOneofCase.Command:
                                if (connected.CommandRegistrations.ActiveHandlers.TryGetValue(
                                        new CommandName(receive.Message.Command.Name), out var handler))
                                {
                                    if (receive.Message.InstructionId != null)
                                    {
                                        await connected.Stream.RequestStream.WriteAsync(
                                            new CommandProviderOutbound
                                            {
                                                Ack = new InstructionAck
                                                {
                                                    InstructionId = receive.Message.InstructionId,
                                                    Success = true
                                                }
                                            }).ConfigureAwait(false);
                                    }

                                    var token = connected.CommandTasks.NextToken();
                                    connected.CommandTasks.Add(token,
                                        Task.Run(async () =>
                                        {
                                            try
                                            {
                                                var result = await handler(receive.Message.Command, ct).ConfigureAwait(false);
                                                var response =
                                                    new CommandResponse(result)
                                                    {
                                                        RequestIdentifier =
                                                            receive.Message.Command
                                                                .MessageIdentifier
                                                    };
                                                await _actor.TellAsync(
                                                    new Message.SendCommandResponse(
                                                        token,
                                                        response));
                                            }
                                            catch (Exception exception)
                                            {
                                                var response = new CommandResponse
                                                {
                                                    ErrorCode = ErrorCategory
                                                        .CommandExecutionError.ToString(),
                                                    ErrorMessage = new ErrorMessage
                                                    {
                                                        Details =
                                                        {
                                                            exception.ToString() ?? ""
                                                        },
                                                        Location = "Client",
                                                        Message = exception.Message ?? ""
                                                    },
                                                    RequestIdentifier =
                                                        receive.Message.Command
                                                            .MessageIdentifier
                                                };
                                                await _actor.TellAsync(
                                                    new Message.SendCommandResponse(
                                                        token,
                                                        response));
                                            }
                                        }, ct));
                                }
                                else
                                {
                                    if (receive.Message.InstructionId != null)
                                    {
                                        await connected.Stream.RequestStream.WriteAsync(
                                            new CommandProviderOutbound
                                            {
                                                Ack = new InstructionAck
                                                {
                                                    InstructionId = receive.Message.InstructionId,
                                                    Success = false,
                                                    Error = new ErrorMessage()
                                                }
                                            }
                                        ).ConfigureAwait(false);
                                    }

                                    await connected.Stream.RequestStream.WriteAsync(
                                        new CommandProviderOutbound
                                        {
                                            CommandResponse = new CommandResponse
                                            {
                                                RequestIdentifier =
                                                    receive.Message.Command.MessageIdentifier,
                                                ErrorCode = ErrorCategory.NoHandlerForCommand.ToString(),
                                                ErrorMessage = new ErrorMessage
                                                    { Message = "No Handler for command" }
                                            }
                                        }).ConfigureAwait(false);
                                    
                                    if (connected.Flow.Increment())
                                    {
                                        await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                        {
                                            FlowControl = new FlowControl
                                            {
                                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                Permits = connected.Flow.Threshold.ToInt64()
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                }
                                break;
                        }


                        break;
                }

                break;

            case Message.SendCommandResponse send:
                switch (state)
                {
                    case State.Connected connected:
                        if (connected.CommandTasks.TryRemove(send.Token, out var commandTask))
                        {
                            await commandTask.ConfigureAwait(false);
                            
                            await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                            {
                                CommandResponse = send.CommandResponse
                            }).ConfigureAwait(false);

                            if (connected.Flow.Increment())
                            {
                                await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                {
                                    FlowControl = new FlowControl
                                    {
                                        ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                        Permits = connected.Flow.Threshold.ToInt64()
                                    }
                                }).ConfigureAwait(false);
                            }
                        }

                        break;
                }

                break;
        }

        return state;
    }

    public ClientIdentity ClientIdentity { get; }
    public Context Context { get; }
    public CommandService.CommandServiceClient Service { get; }

    private record RegisteredCommand(RegistrationId CommandRegistrationId, CommandName CommandName);
    
    private record Message
    {
        public record Connect : Message;

        public record ReceiveCommandProviderInbound(CommandProviderInbound Message) : Message;

        public record SendCommandResponse(long Token, CommandResponse CommandResponse) : Message;

        public record RegisterCommandHandler(
            RegistrationId RegistrationId,
            Func<Command, CancellationToken, Task<CommandResponse>> Handler,
            LoadFactor LoadFactor,
            RegisteredCommand[] RegisteredCommands,
            CountdownCompletionSource CompletionSource) : Message;

        public record UnregisterCommandHandler(
            RegistrationId RegistrationId,
            RegisteredCommand[] RegisteredCommands,
            CountdownCompletionSource CompletionSource) : Message;

        public record Reconnect : Message;

        public record Disconnect : Message;
    }

    private record State(CommandRegistrations CommandRegistrations,
        FlowController Flow,
        TaskRunCache CommandTasks,
        BackoffPolicy ConnectBackoffPolicy)
    {
        public record Disconnected(
            CommandRegistrations CommandRegistrations,
            FlowController Flow,
            TaskRunCache CommandTasks,
            BackoffPolicy ConnectBackoffPolicy) : State(CommandRegistrations,
            Flow,
            CommandTasks,
            ConnectBackoffPolicy);

        public record Connected(
            AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound> Stream,
            Task ConsumeResponseStreamLoop,
            CommandRegistrations CommandRegistrations,
            FlowController Flow,
            TaskRunCache CommandTasks,
            BackoffPolicy ConnectBackoffPolicy) : State(CommandRegistrations,
            Flow,
            CommandTasks,
            ConnectBackoffPolicy);
    }

    public bool IsConnected => _actor.State is State.Connected;

    public async ValueTask Reconnect()
    {
        await _actor.TellAsync(new Message.Reconnect()).ConfigureAwait(false);
    }
    
    public async Task<ICommandHandlerRegistration> RegisterCommandHandler(
        Func<Command, CancellationToken, Task<CommandResponse>> handler,
        LoadFactor loadFactor,
        params CommandName[] commandNames)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (commandNames == null) throw new ArgumentNullException(nameof(commandNames));
        if (commandNames.Length == 0)
            throw new ArgumentException("The command names requires at least one command name to be specified",
                nameof(commandNames));

        var registrationId = RegistrationId.New();
        var registeredCommands = commandNames.Select(name => new RegisteredCommand(RegistrationId.New(), name)).ToArray();
        var subscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
        await _actor.TellAsync(new Message.RegisterCommandHandler(
            registrationId, 
            handler, 
            loadFactor,
            registeredCommands, 
            subscribeCompletionSource)).ConfigureAwait(false);
        return new CommandHandlerRegistration(subscribeCompletionSource.Completion, async () =>
        {
            var unsubscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
            await _actor.TellAsync(
                new Message.UnregisterCommandHandler(
                    registrationId,
                    registeredCommands,
                    unsubscribeCompletionSource)).ConfigureAwait(false);
            await unsubscribeCompletionSource.Completion.ConfigureAwait(false);
        });
    }

    public async Task<CommandResponse> SendCommand(Command command, CancellationToken ct)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        
        using var activity = 
            Telemetry.Source.StartActivity("CommandChannel.SendCommand")
                .SetClientIdTag(ClientIdentity.ClientInstanceId)
                .SetComponentNameTag(ClientIdentity.ComponentName)
                .SetContextTag(Context);
        
        var request = new Command(command)
        {
            ClientId = ClientIdentity.ClientInstanceId.ToString(),
            ComponentName = ClientIdentity.ComponentName.ToString()
        };
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = Guid.NewGuid().ToString("D");
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
                ClientIdentity,
                ErrorCategory.CommandDispatchError,
                "An error occurred while attempting to dispatch a command",
                exception);
        }
    }

    public ValueTask DisposeAsync() => _actor.DisposeAsync();
}