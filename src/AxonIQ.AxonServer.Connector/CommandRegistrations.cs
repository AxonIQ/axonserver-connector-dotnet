using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public class CommandRegistrations
{
    public record RegisteredCommand(
        RegistrationId CommandHandlerRegistrationId,
        RegistrationId CommandRegistrationId,
        CommandName Command);

    public record RegisteredCommandHandler(
        RegistrationId CommandHandlerRegistrationId,
        LoadFactor LoadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler);

    public CommandRegistrations(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
        
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    public Dictionary<RegistrationId, RegisteredCommandHandler> AllCommandHandlers { get; } = new();
    public Dictionary<RegistrationId, RegisteredCommand> AllSubscriptions { get; } = new();
    public Dictionary<RegistrationId, CountdownCompletionSource> SubscribeCompletionSources { get; } = new();
    public Dictionary<RegistrationId, CountdownCompletionSource> UnsubscribeCompletionSources { get; } = new();
    public Dictionary<CommandName, RegistrationId> ActiveRegistrations { get; } = new();
    public Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> ActiveHandlers { get; } = new();
    public Dictionary<InstructionId, RegistrationId> SubscribeInstructions { get; } = new();
    public Dictionary<InstructionId, RegistrationId>  UnsubscribeInstructions { get; } = new();
    public HashSet<RegistrationId> SupersededCommandRegistrations { get; } = new();

    public void RegisterCommandHandler(
        RegistrationId commandHandlerRegistrationId,
        CountdownCompletionSource subscribeCompletionSource,
        LoadFactor loadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> handler)
    {
        if (!AllCommandHandlers.ContainsKey(commandHandlerRegistrationId))
        {
            if (!SubscribeCompletionSources.ContainsKey(commandHandlerRegistrationId))
            {
                SubscribeCompletionSources.Add(commandHandlerRegistrationId, subscribeCompletionSource);
            }
            
            AllCommandHandlers.Add(commandHandlerRegistrationId, new RegisteredCommandHandler(commandHandlerRegistrationId, loadFactor, handler));
        }
    }
        
    public InstructionId SubscribeToCommand(
        RegistrationId commandHandlerRegistrationId,
        RegistrationId commandRegistrationId,
        CommandName command)
    {
        if (!AllSubscriptions.ContainsKey(commandRegistrationId))
        {
            AllSubscriptions.Add(commandRegistrationId, new RegisteredCommand(
                commandHandlerRegistrationId,
                commandRegistrationId,
                command
            ));
        }

        foreach (var otherSubscriptionId in SubscribeInstructions.Values)
        {
            if (otherSubscriptionId != commandRegistrationId &&
                AllSubscriptions.TryGetValue(otherSubscriptionId, out var otherSubscription) && otherSubscription.Command.Equals(command))
            {
                SupersededCommandRegistrations.Add(otherSubscriptionId);
            }
        }
            
        var instructionId = InstructionId.New();
        SubscribeInstructions.Add(instructionId, commandRegistrationId);
        return instructionId;
    }

    public void Acknowledge(InstructionAck acknowledgement)
    {
        var instructionId = new InstructionId(acknowledgement.InstructionId);
            
        if (SubscribeInstructions.TryGetValue(instructionId, out var subscribeSubscriptionId)
            && AllSubscriptions.TryGetValue(subscribeSubscriptionId, out var subscribeSubscription))
        {
            SubscribeInstructions.Remove(instructionId);
                
            if (SubscribeCompletionSources.TryGetValue(subscribeSubscription.CommandHandlerRegistrationId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        SubscribeCompletionSources.Remove(subscribeSubscription.CommandHandlerRegistrationId);
                    }
                }
                else
                {
                    var exception = new AxonServerException(ClientIdentity,
                        ErrorCategory.Parse(acknowledgement.Error.ErrorCode),
                        acknowledgement.Error.Message,
                        acknowledgement.Error.Location,
                        acknowledgement.Error.Details);
                    if (completionSource.SignalFault(exception))
                    {
                        SubscribeCompletionSources.Remove(subscribeSubscription.CommandHandlerRegistrationId);
                    }
                }
            }

            if (SupersededCommandRegistrations.Contains(subscribeSubscriptionId))
            {
                AllSubscriptions.Remove(subscribeSubscriptionId);
                SupersededCommandRegistrations.Remove(subscribeSubscriptionId);
            }
            else
            {
                if (acknowledgement.Success && AllCommandHandlers.TryGetValue(subscribeSubscription.CommandHandlerRegistrationId, out var commandHandler))
                {
                    if (ActiveRegistrations.TryGetValue(subscribeSubscription.Command,
                            out var activeSubscriptionId))
                    {
                        ActiveRegistrations[subscribeSubscription.Command] = subscribeSubscription.CommandRegistrationId;
                        ActiveHandlers[subscribeSubscription.Command] = commandHandler.Handler;
                        AllSubscriptions.Remove(activeSubscriptionId);
                    }
                    else
                    {
                        ActiveRegistrations.Add(subscribeSubscription.Command, subscribeSubscriptionId);
                        ActiveHandlers.Add(subscribeSubscription.Command, commandHandler.Handler);
                    }
                }
            }
        }
        else if (UnsubscribeInstructions.TryGetValue(instructionId, out var unsubscribeSubscriptionId)
                 && AllSubscriptions.TryGetValue(unsubscribeSubscriptionId, out var unsubscribeSubscription))
        {
            UnsubscribeInstructions.Remove(instructionId);
            AllSubscriptions.Remove(unsubscribeSubscriptionId);
            
            if (UnsubscribeCompletionSources.TryGetValue(unsubscribeSubscription.CommandHandlerRegistrationId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.CommandHandlerRegistrationId);
                    }
                }
                else
                {
                    var exception = new AxonServerException(ClientIdentity,
                        ErrorCategory.Parse(acknowledgement.Error.ErrorCode),
                        acknowledgement.Error.Message,
                        acknowledgement.Error.Location,
                        acknowledgement.Error.Details);
                    if (completionSource.SignalFault(exception))
                    {
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.CommandHandlerRegistrationId);
                    }
                }
            }
        }
    }
    
    public void UnregisterCommandHandler(
        RegistrationId commandHandlerRegistrationId,
        CountdownCompletionSource unsubscribeCompletionSource)
    {
        if (AllCommandHandlers.ContainsKey(commandHandlerRegistrationId))
        {
            if (!UnsubscribeCompletionSources.ContainsKey(commandHandlerRegistrationId))
            {
                UnsubscribeCompletionSources.Add(commandHandlerRegistrationId, unsubscribeCompletionSource);
            }
            
            AllCommandHandlers.Remove(commandHandlerRegistrationId);
        }
    }

    public InstructionId? UnsubscribeFromCommand(RegistrationId commandRegistrationId)
    {
        if (AllSubscriptions.TryGetValue(commandRegistrationId, out var subscription))
        {
            if (ActiveRegistrations.TryGetValue(subscription.Command, out var activeSubscriptionId) &&
                activeSubscriptionId == commandRegistrationId)
            {
                ActiveRegistrations.Remove(subscription.Command);
                ActiveHandlers.Remove(subscription.Command);
                
                var instructionId = InstructionId.New();
                UnsubscribeInstructions.Add(instructionId, commandRegistrationId);
                return instructionId;
            }
        }

        return default;
    }
}

// RegisterCommandHandler
//     if not known
//         -> add to AllCommandHandlers
//         -> add to SubscribeCompletionSources
//
// SubscribeToCommand
//     if not known
//         -> add to AllSubscriptions
//     -> add all other unacknowledged subscribe instructions as superseded subscriptions
//     -> add to SubscribeInstructions
//
// UnregisterCommandHandler
//     if known
//         -> add to UnsubscribeCompletionSources
//
// UnsubscribeFromCommand
//     if known
//         if no other subscribe or unsubscribe for the same command name
//             -> add to UnsubscribeInstructions
//
// Acknowledge
//     if known subscribe instruction
//         -> remove instruction
//         if completion source available
//             if success
//                 -> signal success
//                 if last notice then
//                     -> remove completion source
//             else
//                 -> signal fault
//                 if last notice then
//                     -> remove completion source
//         if superseded
//             -> remove from AllSubscriptions
//             -> remove from SupersededSubscriptions
//         else if success and associated command handler is known
//             if active subscription
//                 -> replace active subscription
//                 -> replace active handler
//             else
//                 -> add active subscription
//                 -> add active handler
//     if known unsubscribe instruction
//         -> remove instruction
//         if completion source available
//             if success
//                 -> signal success
//                 if last notice then
//                     -> remove completion source
//             else
//                 -> signal fault
//                 if last notice then
//                     -> remove completion source