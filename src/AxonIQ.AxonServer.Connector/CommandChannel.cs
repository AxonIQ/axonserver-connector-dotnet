/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class CommandChannel : ICommandChannel, IAsyncDisposable
{
    private readonly Context _context;
    private readonly PermitCount _permits;
    private readonly PermitCount _permitsBatch;
    private readonly ILogger<CommandChannel> _logger;

    private readonly Channel<Protocol> _channel;
    private readonly CancellationTokenSource _channelCancellation;
    private readonly Task _protocol;

    private State _state;

    public CommandChannel(
        ClientIdentity clientIdentity,
        Context context,
        Func<DateTimeOffset> clock,
        CallInvoker callInvoker,
        PermitCount permits,
        PermitCount permitsBatch,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        ClientIdentity = clientIdentity;
        Clock = clock;
        _context = context;
        _permits = permits;
        _permitsBatch = permitsBatch;
        Service = new CommandService.CommandServiceClient(callInvoker);
        _logger = loggerFactory.CreateLogger<CommandChannel>();
        _permits = permits;
        _permitsBatch = permitsBatch;

        _state = new State.Disconnected();
        _channel = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _channelCancellation = new CancellationTokenSource();
        _protocol = RunChannelProtocol(_channelCancellation.Token);
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<CommandProviderInbound> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(ct))
            {
                await _channel.Writer.WriteAsync(new Protocol.ReceiveCommandProviderInbound(response), ct);
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception,
                "The command channel instruction stream is no longer being read because an object got disposed");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The command channel instruction stream is no longer being read because a task was cancelled");
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
                "The command channel instruction stream is no longer being read because of an unexpected exception");
        }
    }

    private async Task EnsureConnected(CancellationToken ct)
    {
        switch (_state)
        {
            case State.Disconnected:
                try
                {
                    var stream = Service.OpenStream(cancellationToken: ct);
                    if (stream != null)
                    {
                        _logger.LogInformation(
                            "Opened command stream for context '{Context}'",
                            _context);

                        await stream.RequestStream.WriteAsync(new CommandProviderOutbound
                        {
                            FlowControl = new FlowControl
                            {
                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                Permits = _permits.ToInt64()
                            }
                        });

                        _state = new State.Connected(
                            stream,
                            ConsumeResponseStream(stream.ResponseStream, ct)
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not open command stream for context '{Context}'",
                            _context);
                    }
                }
                catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                {
                    _logger.LogWarning(
                        "Could not open command stream for context '{Context}': no connection to AxonServer",
                        _context.ToString());
                }

                break;
            case State.Connected:
                _logger.LogDebug("CommandChannel for context '{Context}' is already connected",
                    _context.ToString());
                break;
        }
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        var subscriptions = new CommandSubscriptions(ClientIdentity, Clock);
        var flowController = new FlowController(_permitsBatch);
        var commandsInFlight = new Dictionary<Command, Task>();
        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                while (_channel.Reader.TryRead(out var message))
                {
                    _logger.LogDebug("Began {Message} when {State}", message.ToString(), _state.ToString());
                    switch (message)
                    {
                        case Protocol.Connect:
                            await EnsureConnected(ct);

                            break;
                        case Protocol.SubscribeCommandHandler subscribe:
                            await EnsureConnected(ct);
                            switch (_state)
                            {
                                case State.Connected connected:
                                    subscriptions.RegisterCommandHandler(
                                        subscribe.CommandHandlerId,
                                        subscribe.CompletionSource,
                                        subscribe.LoadFactor,
                                        subscribe.Handler);

                                    foreach (var (subscriptionId, command) in subscribe.SubscribedCommands)
                                    {
                                        _logger.LogInformation(
                                            "Registered handler for command '{CommandName}' in context '{Context}'",
                                            command.ToString(), _context.ToString());

                                        var instructionId = subscriptions.SubscribeToCommand(subscribe.CommandHandlerId,
                                            subscriptionId, command);
                                        var request = new CommandProviderOutbound
                                        {
                                            InstructionId = instructionId.ToString(),
                                            Subscribe = new CommandSubscription
                                            {
                                                MessageId = instructionId.ToString(),
                                                Command = command.ToString(),
                                                LoadFactor = subscribe.LoadFactor.ToInt32(),
                                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                ComponentName = ClientIdentity.ComponentName.ToString()
                                            }
                                        };
                                        await connected.Stream.RequestStream.WriteAsync(request);
                                    }

                                    break;
                                case State.Disconnected:
                                    if (!subscribe.CompletionSource.Fault(
                                            new AxonServerException(
                                                ClientIdentity,
                                                ErrorCategory.Other,
                                                "Unable to subscribe commands and handler: no connection to AxonServer")))
                                    {
                                        _logger.LogWarning(
                                            "Could not fault the subscribe completion source of command handler '{CommandHandlerId}'",
                                            subscribe.CommandHandlerId.ToString());
                                    }

                                    break;
                            }

                            break;
                        case Protocol.UnsubscribeCommandHandler unsubscribe:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    subscriptions.UnregisterCommandHandler(
                                        unsubscribe.CommandHandlerId,
                                        unsubscribe.CompletionSource);
                                    
                                    foreach (var (subscriptionId, command) in unsubscribe.SubscribedCommands)
                                    {
                                        var instructionId = subscriptions.UnsubscribeFromCommand(subscriptionId);
                                        if (instructionId.HasValue)
                                        {
                                            _logger.LogInformation(
                                                "Unregistered handler for command '{CommandName}' in context '{Context}'",
                                                command.ToString(), _context.ToString());

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
                                            await connected.Stream.RequestStream.WriteAsync(request);
                                        }
                                    }

                                    break;
                                case State.Disconnected:
                                    if (!unsubscribe.CompletionSource.Fault(
                                            new AxonServerException(
                                                ClientIdentity,
                                                ErrorCategory.Other,
                                                "Unable to unsubscribe commands and handler: no connection to AxonServer")))
                                    {
                                        _logger.LogWarning(
                                            "Could not fault the unsubscribe completion source of command handler '{CommandHandlerId}'",
                                            unsubscribe.CommandHandlerId.ToString());
                                    }

                                    break;
                            }

                            break;
                        case Protocol.Reconnect:
                            switch (_state)
                            {
                                case State.Disconnected:
                                    break;
                            }

                            break;
                        case Protocol.Disconnect:
                            switch (_state)
                            {
                                case State.Disconnected:
                                    break;
                            }

                            break;
                        case Protocol.ReceiveCommandProviderInbound receive:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    switch (receive.Message.RequestCase)
                                    {
                                        case CommandProviderInbound.RequestOneofCase.None:
                                            break;
                                        case CommandProviderInbound.RequestOneofCase.Ack:
                                            subscriptions.Acknowledge(receive.Message.Ack);
                                            
                                            if (flowController.Increment())
                                            {
                                                await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                                {
                                                    FlowControl = new FlowControl
                                                    {
                                                        ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                        Permits = _permitsBatch.ToInt64()
                                                    }
                                                });
                                            }
                                            break;
                                        case CommandProviderInbound.RequestOneofCase.Command:
                                            if (subscriptions.ActiveHandlers.TryGetValue(
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
                                                        }
                                                    );
                                                }

                                                commandsInFlight.Add(receive.Message.Command,
                                                    handler(receive.Message.Command, ct)
                                                    .ContinueWith(
                                                        continuation =>
                                                        {
                                                            if (!continuation.IsCanceled)
                                                            {
                                                                if (continuation.IsFaulted)
                                                                {
                                                                    var response = new CommandResponse
                                                                    {
                                                                        ErrorCode = ErrorCategory.CommandExecutionError.ToString(),
                                                                        ErrorMessage = new ErrorMessage
                                                                        {
                                                                            Details =
                                                                            {
                                                                                continuation.Exception?.ToString() ?? ""
                                                                            },
                                                                            Location = "Client",
                                                                            Message = continuation.Exception?.Message ?? ""
                                                                        },
                                                                        RequestIdentifier = receive.Message.Command.MessageIdentifier
                                                                    };
                                                                    if (!_channel.Writer.TryWrite(new Protocol.SendCommandResponse(receive.Message.Command, response)))
                                                                    {
                                                                        _logger.LogWarning(
                                                                            "Could not tell the command channel to send the command response after handling a command was completed faulty because the channel refused to accept the message");
                                                                    }
                                                                }
                                                                else if (continuation.IsCompletedSuccessfully)
                                                                {
                                                                    var response =
                                                                        new CommandResponse(continuation.Result)
                                                                        {
                                                                            RequestIdentifier = receive.Message.Command.MessageIdentifier
                                                                        };
                                                                    if (!_channel.Writer.TryWrite(new Protocol.SendCommandResponse(receive.Message.Command, response)))
                                                                    {
                                                                        _logger.LogWarning(
                                                                            "Could not tell the command channel to send the command response after handling a command was completed successfully because the channel refused to accept the message");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    _logger.LogWarning(
                                                                        "Handling a command completed in an unexpected way and a response will not be sent");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                _logger.LogDebug(
                                                                    "Handling a command was cancelled and a response will not be sent");
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
                                                    );
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
                                                    });
                                                
                                                if (flowController.Increment())
                                                {
                                                    await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                                    {
                                                        FlowControl = new FlowControl
                                                        {
                                                            ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                            Permits = _permitsBatch.ToInt64()
                                                        }
                                                    });
                                                }
                                            }
                                            break;
                                    }


                                    break;
                            }

                            break;

                        case Protocol.SendCommandResponse send:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    if (commandsInFlight.Remove(send.Command))
                                    {
                                        await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                        {
                                            CommandResponse = send.CommandResponse
                                        });

                                        if (flowController.Increment())
                                        {
                                            await connected.Stream.RequestStream.WriteAsync(new CommandProviderOutbound
                                            {
                                                FlowControl = new FlowControl
                                                {
                                                    ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                    Permits = _permitsBatch.ToInt64()
                                                }
                                            });
                                        }
                                    }

                                    break;
                            }

                            break;
                    }

                    _logger.LogDebug("Completed {Message} with {State}", message.ToString(), _state.ToString());
                }
            }
        }
        catch (RpcException exception) when (exception.Status.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug(exception,
                "Command channel protocol loop is exciting because an rpc call was cancelled");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Command channel protocol loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Command channel protocol loop is exciting because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Command channel protocol loop is exciting because of an unexpected exception");
        }
    }

    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    public CommandService.CommandServiceClient Service { get; }

    private record SubscribedCommand(SubscriptionId SubscriptionId, CommandName CommandName);
    
    private record Protocol
    {
        public record Connect : Protocol;

        public record ReceiveCommandProviderInbound(CommandProviderInbound Message) : Protocol;

        public record SendCommandResponse(Command Command, CommandResponse CommandResponse) : Protocol;

        public record SubscribeCommandHandler(
            CommandHandlerId CommandHandlerId,
            Func<Command, CancellationToken, Task<CommandResponse>> Handler,
            LoadFactor LoadFactor,
            SubscribedCommand[] SubscribedCommands,
            CountdownCompletionSource CompletionSource) : Protocol;

        public record UnsubscribeCommandHandler(
            CommandHandlerId CommandHandlerId,
            SubscribedCommand[] SubscribedCommands,
            CountdownCompletionSource CompletionSource) : Protocol;

        public record Reconnect : Protocol;

        public record Disconnect : Protocol;
    }

    private record State
    {
        public record Disconnected : State;

        public record Connected(
            AsyncDuplexStreamingCall<CommandProviderOutbound, CommandProviderInbound> Stream,
            Task ConsumeResponseStreamLoop) : State;
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

        var commandHandlerId = CommandHandlerId.New();
        var subscribedCommands = commandNames.Select(name => new SubscribedCommand(SubscriptionId.New(), name)).ToArray();
        var subscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
        await _channel.Writer.WriteAsync(new Protocol.SubscribeCommandHandler(
            commandHandlerId, 
            handler, 
            loadFactor,
            subscribedCommands, 
            subscribeCompletionSource));
        return new CommandHandlerRegistration(subscribeCompletionSource.Completion, async () =>
        {
            var unsubscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
            await _channel.Writer.WriteAsync(
                new Protocol.UnsubscribeCommandHandler(
                    commandHandlerId,
                    subscribedCommands,
                    unsubscribeCompletionSource));
            await unsubscribeCompletionSource.Completion;
        });
    }

    public async Task<CommandResponse> SendCommand(Command command, CancellationToken ct)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
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

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Dispatching the command {Command} with message identifier {MessageIdentifier}",
                request.Name, request.MessageIdentifier);
        }

        try
        {
            return await Service.DispatchAsync(request, cancellationToken: ct);
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

    public async ValueTask DisposeAsync()
    {
        _channelCancellation.Cancel();
        _channel.Writer.Complete();
        await _channel.Reader.Completion;
        await _protocol;
        _channelCancellation.Dispose();
        _protocol.Dispose();
    }
}