using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public class CommandSubscriptions
{
    public record Subscription(
        CommandHandlerId CommandHandlerId,
        SubscriptionId SubscriptionId,
        CommandName Command);

    public record CommandHandler(
        CommandHandlerId CommandHandlerId,
        LoadFactor LoadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler);

    public CommandSubscriptions(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
        
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    public Dictionary<CommandHandlerId, CommandHandler> AllCommandHandlers { get; } = new();
    public Dictionary<SubscriptionId, Subscription> AllSubscriptions { get; } = new();
    public Dictionary<CommandHandlerId, CountdownCompletionSource> SubscribeCompletionSources { get; } = new();
    public Dictionary<CommandHandlerId, CountdownCompletionSource> UnsubscribeCompletionSources { get; } = new();
    public Dictionary<CommandName, SubscriptionId> ActiveSubscriptions { get; } = new();
    public Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> ActiveHandlers { get; } = new();
    public Dictionary<InstructionId, SubscriptionId> SubscribeInstructions { get; } = new();
    public Dictionary<InstructionId, SubscriptionId>  UnsubscribeInstructions { get; } = new();
    public HashSet<SubscriptionId> SupersededSubscriptions { get; } = new();

    public void RegisterCommandHandler(
        CommandHandlerId commandHandlerId,
        CountdownCompletionSource subscribeCompletionSource,
        LoadFactor loadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> handler)
    {
        if (!AllCommandHandlers.ContainsKey(commandHandlerId))
        {
            if (!SubscribeCompletionSources.ContainsKey(commandHandlerId))
            {
                SubscribeCompletionSources.Add(commandHandlerId, subscribeCompletionSource);
            }
            
            AllCommandHandlers.Add(commandHandlerId, new CommandHandler(commandHandlerId, loadFactor, handler));
        }
    }
        
    public InstructionId SubscribeToCommand(
        SubscriptionId subscriptionId,
        CommandHandlerId commandHandlerId,
        CommandName command)
    {
        if (!AllSubscriptions.ContainsKey(subscriptionId))
        {
            AllSubscriptions.Add(subscriptionId, new Subscription(
                commandHandlerId,
                subscriptionId,
                command
            ));
        }

        foreach (var otherSubscriptionId in SubscribeInstructions.Values)
        {
            if (otherSubscriptionId != subscriptionId &&
                AllSubscriptions.TryGetValue(otherSubscriptionId, out var otherSubscription) && otherSubscription.Command.Equals(command))
            {
                SupersededSubscriptions.Add(otherSubscriptionId);
            }
        }
            
        var instructionId = InstructionId.New();
        SubscribeInstructions.Add(instructionId, subscriptionId);
        return instructionId;
    }

    public void Acknowledge(InstructionAck acknowledgement)
    {
        var instructionId = new InstructionId(acknowledgement.InstructionId);
            
        if (SubscribeInstructions.TryGetValue(instructionId, out var subscribeSubscriptionId)
            && AllSubscriptions.TryGetValue(subscribeSubscriptionId, out var subscribeSubscription))
        {
            SubscribeInstructions.Remove(instructionId);
                
            if (SubscribeCompletionSources.TryGetValue(subscribeSubscription.CommandHandlerId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        SubscribeCompletionSources.Remove(subscribeSubscription.CommandHandlerId);
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
                        SubscribeCompletionSources.Remove(subscribeSubscription.CommandHandlerId);
                    }
                }
            }

            if (SupersededSubscriptions.Contains(subscribeSubscriptionId))
            {
                AllSubscriptions.Remove(subscribeSubscriptionId);
                SupersededSubscriptions.Remove(subscribeSubscriptionId);
            }
            else
            {
                if (acknowledgement.Success && AllCommandHandlers.TryGetValue(subscribeSubscription.CommandHandlerId, out var commandHandler))
                {
                    if (ActiveSubscriptions.TryGetValue(subscribeSubscription.Command,
                            out var activeSubscriptionId))
                    {
                        ActiveSubscriptions[subscribeSubscription.Command] = subscribeSubscription.SubscriptionId;
                        ActiveHandlers[subscribeSubscription.Command] = commandHandler.Handler;
                        AllSubscriptions.Remove(activeSubscriptionId);
                    }
                    else
                    {
                        ActiveSubscriptions.Add(subscribeSubscription.Command, subscribeSubscriptionId);
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
            
            if (UnsubscribeCompletionSources.TryGetValue(unsubscribeSubscription.CommandHandlerId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.CommandHandlerId);
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
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.CommandHandlerId);
                    }
                }
            }
        }
    }
    
    public void UnregisterCommandHandler(
        CommandHandlerId commandHandlerId,
        CountdownCompletionSource unsubscribeCompletionSource)
    {
        if (AllCommandHandlers.ContainsKey(commandHandlerId))
        {
            if (!UnsubscribeCompletionSources.ContainsKey(commandHandlerId))
            {
                UnsubscribeCompletionSources.Add(commandHandlerId, unsubscribeCompletionSource);
            }
            
            AllCommandHandlers.Remove(commandHandlerId);
        }
    }

    public InstructionId? UnsubscribeFromCommand(SubscriptionId subscriptionId)
    {
        if (AllSubscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            if (ActiveSubscriptions.TryGetValue(subscription.Command, out var activeSubscriptionId) &&
                activeSubscriptionId == subscriptionId)
            {
                ActiveSubscriptions.Remove(subscription.Command);
                ActiveHandlers.Remove(subscription.Command);
                
                var instructionId = InstructionId.New();
                UnsubscribeInstructions.Add(instructionId, subscriptionId);
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