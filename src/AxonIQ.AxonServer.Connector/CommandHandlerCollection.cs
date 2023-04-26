using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

internal class CommandHandlerCollection
{
    private record RegisteredCommand(
        CommandName CommandName,
        LoadFactor LoadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler);

    private record SubscribeCommand(RegisteredCommandId RegisteredCommandId, DateTimeOffset Since);
    private record UnsubscribeCommand(RegisteredCommandId RegisteredCommandId, DateTimeOffset Since);

    private readonly Dictionary<RegisteredCommandId, RegisteredCommand> _registeredCommands;
    private readonly Dictionary<InstructionId, SubscribeCommand> _subscribes;
    private readonly Dictionary<InstructionId, UnsubscribeCommand> _unsubscribes;

    private readonly Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> _subscribedCommandHandlers;
    
    private readonly Dictionary<RegisteredCommandId, TaskCompletionSource> _subscribeCompletionSources;
    private readonly Dictionary<RegisteredCommandId, TaskCompletionSource> _unsubscribeCompletionSources;

    public CommandHandlerCollection(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        
        _registeredCommands = new Dictionary<RegisteredCommandId, RegisteredCommand>();
        _subscribes = new Dictionary<InstructionId, SubscribeCommand>();
        _unsubscribes = new Dictionary<InstructionId, UnsubscribeCommand>();

        _subscribedCommandHandlers = new Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>>();
         _subscribeCompletionSources = new Dictionary<RegisteredCommandId, TaskCompletionSource>();
        _unsubscribeCompletionSources = new Dictionary<RegisteredCommandId, TaskCompletionSource>();
    }
    
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }

    public bool HasRegisteredCommands => _registeredCommands.Count != 0;

    public IReadOnlyCollection<RegisteredCommandId> RegisteredCommands => _registeredCommands.Keys;

    public void RegisterCommandHandler(
        RegisteredCommandId id,
        CommandName name,
        LoadFactor loadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> handler)
    {
        _registeredCommands.Add(id, new RegisteredCommand(name, loadFactor, handler));
    }
    
    public void RegisterCommandSubscribeCompletionSource(RegisteredCommandId id, TaskCompletionSource source)
    {
        _subscribeCompletionSources.Add(id, source);
    }
    
    public void RegisterCommandUnsubscribeCompletionSource(RegisteredCommandId id, TaskCompletionSource source)
    {
        _unsubscribeCompletionSources.Add(id, source);
    }
    
    public void UnregisterCommandHandler(RegisteredCommandId id)
    {
        _registeredCommands.Remove(id);
    }
    
    public CommandProviderOutbound BeginSubscribeToCommandInstruction(RegisteredCommandId id)
    {
        if (!_registeredCommands.TryGetValue(id, out var registeredCommand))
        {
            throw new InvalidOperationException($"The command with identifier {id.ToString()} is not registered.");
        }
        var instructionId = InstructionId.New();
        _subscribes.Add(instructionId, new SubscribeCommand(id, Clock()));
        var message = new CommandProviderOutbound
        {
            InstructionId = instructionId.ToString(),
            Subscribe = new CommandSubscription
            {
                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                ComponentName = ClientIdentity.ComponentName.ToString(),
                LoadFactor = registeredCommand.LoadFactor.ToInt32(),
                Command = registeredCommand.CommandName.ToString(),
                MessageId = instructionId.ToString(),
            }
        };
        return message;
    }

    public CommandProviderOutbound BeginUnsubscribeFromCommandInstruction(RegisteredCommandId id)
    {
        if (!_registeredCommands.TryGetValue(id, out var registeredCommand))
        {
            throw new InvalidOperationException($"The command with identifier {id.ToString()} is not registered.");
        }
        var instructionId = InstructionId.New();
        _unsubscribes.Add(instructionId, new UnsubscribeCommand(id, Clock()));
        var message = new CommandProviderOutbound
        {
            InstructionId = instructionId.ToString(),
            Unsubscribe = new CommandSubscription
            {
                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                ComponentName = ClientIdentity.ComponentName.ToString(),
                Command = registeredCommand.CommandName.ToString(),
                MessageId = instructionId.ToString(),
            }
        };
        return message;
    }

    public bool TryCompleteSubscribeToCommandInstruction(InstructionAck acknowledgement)
    {
        if (acknowledgement == null) throw new ArgumentNullException(nameof(acknowledgement));
        
        if (InstructionId.TryParse(acknowledgement.InstructionId, out var id))
        {
            if (!_subscribes.Remove(id, out var subscribe) || !_registeredCommands.TryGetValue(subscribe.RegisteredCommandId, out var registeredCommand))
            {
                return false;
            }

            _subscribedCommandHandlers[registeredCommand.CommandName] = registeredCommand.Handler;

            if (_subscribeCompletionSources.Remove(subscribe.RegisteredCommandId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    completionSource.TrySetResult();
                }
                else
                {
                    completionSource.TrySetException(
                        AxonServerException.FromErrorMessage(ClientIdentity, acknowledgement.Error));
                }
            }

            return true;
        }

        return false;
    }
    
    public bool TryCompleteUnsubscribeFromCommandInstruction(InstructionAck acknowledgement)
    {
        if (acknowledgement == null) throw new ArgumentNullException(nameof(acknowledgement));

        if (InstructionId.TryParse(acknowledgement.InstructionId, out var id))
        {
            if (!_unsubscribes.Remove(id, out var unsubscribe) ||
                !_registeredCommands.TryGetValue(unsubscribe.RegisteredCommandId, out var registeredCommand))
            {
                return false;
            }

            if (_subscribedCommandHandlers
                    .TryGetValue(registeredCommand.CommandName, out var handler) &&
                ReferenceEquals(handler, registeredCommand.Handler))
            {
                _subscribedCommandHandlers.Remove(registeredCommand.CommandName);
            }
            
            if (_unsubscribeCompletionSources.Remove(unsubscribe.RegisteredCommandId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    completionSource.TrySetResult();
                }
                else
                {
                    completionSource.TrySetException(
                        AxonServerException.FromErrorMessage(ClientIdentity, acknowledgement.Error));
                }
            }
            
            _registeredCommands.Remove(unsubscribe.RegisteredCommandId);

            return true;
        }

        return false;
    }

    public void Purge(TimeSpan age)
    {
        var threshold = Clock().Subtract(age);
        
        var overdueSubscribeInstructions =
            _subscribes
                .Where(item => item.Value.Since <= threshold)
                .ToArray();
        foreach (var (instruction, subscribe) in overdueSubscribeInstructions)
        {
            _subscribes.Remove(instruction);
            
            if (_subscribeCompletionSources.Remove(subscribe.RegisteredCommandId, out var completionSource))
            {
                completionSource.TrySetException(
                    new AxonServerException(
                        ClientIdentity, ErrorCategory.Other,
                        $"The subscribe instruction with identifier {instruction.ToString()} was not acknowledged in time."));
            }
        }
        
        var expiredUnsubscribeInstructions =
            _unsubscribes
                .Where(item => item.Value.Since <= threshold)
                .ToArray();
        foreach (var (instruction, unsubscribe) in expiredUnsubscribeInstructions)
        {
            _unsubscribes.Remove(instruction);
            
            if (_unsubscribeCompletionSources.Remove(unsubscribe.RegisteredCommandId, out var completionSource))
            {
                completionSource.TrySetException(
                    new AxonServerException(
                        ClientIdentity, ErrorCategory.Other,
                        $"The unsubscribe instruction with identifier {instruction.ToString()} was not acknowledged in time."));
            }

            _registeredCommands.Remove(unsubscribe.RegisteredCommandId);
        }
    }
    
    public bool TryGetCommandHandler(
        CommandName commandName,
        out Func<Command, CancellationToken, Task<CommandResponse>>? handler)
    {
        return _subscribedCommandHandlers.TryGetValue(commandName, out handler);
    }
}