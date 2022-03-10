using System.Threading.Channels;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class CommandChannel : ICommandChannel, IAsyncDisposable
{
    private readonly Context _context;
    private readonly ILogger<CommandChannel> _logger;
    
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    
    private State _state;

    public CommandChannel(
        ClientIdentity clientIdentity,
        Context context,
        CallInvoker callInvoker,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (callInvoker == null) throw new ArgumentNullException(nameof(callInvoker));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        
        ClientIdentity = clientIdentity;
        _context = context;
        Service = new CommandService.CommandServiceClient(callInvoker);
        _logger = loggerFactory.CreateLogger<CommandChannel>();

        _state = new State.Disconnected();
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }
    
    private async Task ConsumeResponseStream(IAsyncStreamReader<CommandProviderInbound> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(ct))
            {
                await _inbox.Writer.WriteAsync(new Protocol.ReceivedServerMessage(response), ct);
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
            while (await _inbox.Reader.WaitToReadAsync(ct))
            {
                while (_inbox.Reader.TryRead(out var message))
                {
                    _logger.LogDebug("Began {Message} when {State}", message.ToString(), _state.ToString());
                    switch (message)
                    {
                        case Protocol.Connect:
                            switch (_state)
                            {
                                case State.Disconnected:
                                    try
                                    {
                                        var commandProviderStream = Service.OpenStream(cancellationToken: ct);
                                        if (commandProviderStream != null)
                                        {
                                            _logger.LogInformation(
                                                "Connected command stream for context '{Context}'",
                                                _context);

                                            _state = new State.Connected(
                                                commandProviderStream,
                                                ConsumeResponseStream(commandProviderStream.ResponseStream, ct),
                                                new SubscriptionsState2(
                                                    new Dictionary<CommandHandlerId, CountdownCompletionSource>(),
                                                    new Dictionary<SubscriptionId, CommandSubscriptionState>(),
                                                    new Dictionary<InstructionId, SubscriptionId>()));
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
                                            "Could not open command stream for context '{Context}': no connection to AxonServer", _context.ToString());
                                    }
                                    
                                    break;
                                case State.Connected:
                                    _logger.LogDebug("CommandChannel for context '{Context}' is already connected", _context.ToString());
                                    break;
                            }
                            break;
                        case Protocol.SubscribeCommandHandler subscribe:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    connected.Subscriptions.SubscribeCompletionSources.Add(subscribe.CommandHandlerId, subscribe.CompletionSource);
                                    
                                    foreach (var command in subscribe.Commands)
                                    {
                                        var subscriptionId = SubscriptionId.New();
                                        _logger.LogInformation("Registered handler for command '{CommandName}' in context '{Context}'", command.ToString(), _context.ToString());
                                        connected.Subscriptions.All.Add(
                                            subscriptionId,
                                            new CommandSubscriptionState(
                                                subscribe.CommandHandlerId,
                                                subscriptionId,
                                                command,
                                                subscribe.LoadFactor,
                                                subscribe.Handler
                                            )
                                        );
                                        var instructionId = InstructionId.New();
                                        connected.Subscriptions.Subscribing.Add(instructionId, subscriptionId);
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
                                        await connected.CommandProviderStream.RequestStream.WriteAsync(request);
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
                                            "Could not fault the completion source of command handler '{CommandHandlerId}'",
                                            subscribe.CommandHandlerId.ToString());
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
                        case Protocol.ReceivedServerMessage received:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    switch(received.Message.RequestCase)
                                    {
                                        case CommandProviderInbound.RequestOneofCase.None:
                                            break;
                                        case CommandProviderInbound.RequestOneofCase.Ack:
                                            var instructionId = new InstructionId(received.Message.Ack.InstructionId);
                                            if (connected.Subscriptions.Subscribing.TryGetValue(instructionId, out var subscriptionId))
                                            {
                                                if (connected.Subscriptions.All.TryGetValue(subscriptionId, out var subscription))
                                                {
                                                    if (connected.Subscriptions.SubscribeCompletionSources.TryGetValue(subscription.CommandHandlerId, out var completionSource))
                                                    {
                                                        if (received.Message.Ack.Success)
                                                        {
                                                            if (completionSource.SignalSuccess())
                                                            {
                                                                // We can safely remove it since this was the only reason to keep track of it
                                                                connected.Subscriptions.SubscribeCompletionSources.Remove(subscription.CommandHandlerId);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            var exception = new AxonServerException(ClientIdentity,
                                                                ErrorCategory.Parse(
                                                                    received.Message.Ack.Error.ErrorCode),
                                                                received.Message.Ack.Error.Message,
                                                                received.Message.Ack.Error.Location,
                                                                received.Message.Ack.Error.Details);
                                                            if (completionSource.SignalFault(exception))
                                                            {
                                                                // We can safely remove it since this was the only reason to keep track of it
                                                                connected.Subscriptions.SubscribeCompletionSources.Remove(subscription.CommandHandlerId);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            break;
                                        case CommandProviderInbound.RequestOneofCase.Command:
                                            
                                            break;
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
                "Command channel message loop is exciting because an rpc call was cancelled");
        }
        catch (TaskCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Command channel message loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Command channel message loop is exciting because an operation was cancelled");
        }
    }

    public ClientIdentity ClientIdentity { get; }
    public CommandService.CommandServiceClient Service { get; }

    private record Protocol
    {
        public record Connect : Protocol;

        public record ReceivedServerMessage(CommandProviderInbound Message) : Protocol;

        public record SubscribeCommandHandler(
            CommandHandlerId CommandHandlerId, 
            Func<Command, CancellationToken, Task<CommandResponse>> Handler,
            LoadFactor LoadFactor,
            CommandName[] Commands,
            CountdownCompletionSource CompletionSource) : Protocol;
        
        public record UnsubscribeCommandHandler(
            CommandHandlerId CommandHandlerId, 
            CountdownCompletionSource CompletionSource) : Protocol;
        
        public record Reconnect : Protocol;

        public record Disconnect : Protocol;
    }

    private record CommandHandlerSubscription(
        CommandHandlerId CommandHandlerId,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler,
        LoadFactor LoadFactor,
        CommandName[] CommandNames);
    
    private record CommandSubscriptionState(
        CommandHandlerId CommandHandlerId,
        SubscriptionId SubscriptionId,
        CommandName Command,
        LoadFactor LoadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler);
    
    private record SubscriptionsState
        (Dictionary<SubscriptionId, CommandSubscriptionState> All,
            Dictionary<CommandName, SubscriptionId> Active,
            Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> Handlers,
            Dictionary<InstructionId, SubscriptionId> Subscribing,
            Dictionary<InstructionId, SubscriptionId> Unsubscribing,
            HashSet<SubscriptionId> Superseded);

    private record SubscriptionsState2(
        Dictionary<CommandHandlerId, CountdownCompletionSource> SubscribeCompletionSources,
        Dictionary<SubscriptionId, CommandSubscriptionState> All,
        Dictionary<InstructionId, SubscriptionId> Subscribing);
    
    private record State
    {
        public record Disconnected : State;

        public record Connected(
            AsyncDuplexStreamingCall<CommandProviderOutbound,CommandProviderInbound> CommandProviderStream,
            Task ConsumeResponseStreamLoop,
            SubscriptionsState2 Subscriptions) : State;
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
        var subscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
        await _inbox.Writer.WriteAsync(new Protocol.Connect());
        await _inbox.Writer.WriteAsync(new Protocol.SubscribeCommandHandler(commandHandlerId, handler, loadFactor, commandNames, subscribeCompletionSource));
        return new CommandHandlerRegistration(subscribeCompletionSource.Completion, async () =>
        {
            var unsubscribeCompletionSource = new CountdownCompletionSource(commandNames.Length);
            await _inbox.Writer.WriteAsync(new Protocol.UnsubscribeCommandHandler(commandHandlerId, unsubscribeCompletionSource));
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
        try
        {
            return await Service.DispatchAsync(request, cancellationToken: ct);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                ClientIdentity,
                ErrorCategory.CommandDispatchError,
                "An error occurred while attempting to dispatch a message",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion;
        await _protocol;
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
    //
    // private class CommandSubscriptionManagement
    // {
    //     // public Dictionary<SubscriptionId, Subscription> All { get; } = new Dictionary<SubscriptionId, Subscription>();
    //     // public Dictionary<CommandName, SubscriptionId> Active { get; } = new Dictionary<CommandName, SubscriptionId>();
    //     // public Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> Handlers { get; } = new Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>>();
    //     // public Dictionary<InstructionId, (SubscriptionId, CommandName)> Subscribing { get; } = new Dictionary<InstructionId, (SubscriptionId, CommandName)>();
    //     // public Dictionary<InstructionId, (SubscriptionId, CommandName)>  Unsubscribing { get; } = new Dictionary<InstructionId, (SubscriptionId, CommandName)>();
    //     // public HashSet<SubscriptionId> Superseded { get; } = new HashSet<SubscriptionId>();
    //     //
    //     // public void SubscribeCommandSet(
    //     //     SubscriptionId subscriptionId,
    //     //     )
    //
    //     public void SubscribeCommandHandler(
    //         CommandHandlerId commandHandlerId,
    //         Func<Command, CancellationToken, Task<CommandResponse>> handler,
    //         LoadFactor loadFactor,
    //         CommandName[] commands,
    //         CompletionSource completionSource)
    //     {
    //                     
    //     }
    //
    //     public void SubscribeToCommand(
    //         SubscriptionId subscriptionId,
    //         CommandName command,
    //         LoadFactor loadFactor,
    //         Func<Command, CancellationToken, Task<CommandResponse>> handler)
    //     {}
    //     
    //     public void Acknowledge(InstructionAck acknowledgement)
    //     {}
    //     
    //     public void UnsubscribeFromCommand(
    //         SubscriptionId subscriptionId,
    //         CommandName command)
    //     {}
    //
    //     private record CommandState
    //     {
    //         public record Subscribing(SubscriptionId SubscriptionId, CommandName CommandName) : CommandState;
    //
    //         public record Subscribed(SubscriptionId SubscriptionId, CommandName CommandName) : CommandState;
    //     }
    // }
    //
    // private record CommandSetSubscription(
    //     SubscriptionId SubscriptionId,
    //     TaskCompletionSource SubscribeCompletionSource,
    //     CommandName[] Commands,
    //     Func<Command, CancellationToken, Task<CommandResponse>> Handler);
}