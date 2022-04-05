using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public class CommandSubscriptions
{
    public record Subscription(
        CommandHandlerId CommandHandlerId,
        SubscriptionId SubscriptionId,
        CommandName Command,
        LoadFactor LoadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> Handler);

    public CommandSubscriptions(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
        
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    public Dictionary<SubscriptionId, Subscription> AllSubscriptions { get; } = new();
    public Dictionary<CommandHandlerId, CountdownCompletionSource> CompletionSources { get; } = new();
    public Dictionary<CommandName, SubscriptionId> ActiveSubscriptions { get; } = new();
    public Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> ActiveHandlers { get; } = new();
    public Dictionary<InstructionId, SubscriptionId> SubscribeInstructions { get; } = new();
    public Dictionary<InstructionId, SubscriptionId>  UnsubscribingByInstruction { get; } = new();
    public Dictionary<SubscriptionId, InstructionId> SubscribingBySubscription { get; } = new();
    public Dictionary<SubscriptionId, InstructionId>  UnsubscribingBySubscription { get; } = new();
    public HashSet<SubscriptionId> SupersededSubscriptions { get; } = new();

    public void RegisterCommandHandler(
        CommandHandlerId commandHandlerId,
        CountdownCompletionSource completionSource)
    {
        if (!CompletionSources.ContainsKey(commandHandlerId))
        {
            CompletionSources.Add(commandHandlerId, completionSource);
        }
    }
        
    public InstructionId? SubscribeToCommand(
        SubscriptionId subscriptionId,
        CommandHandlerId commandHandlerId,
        CommandName command,
        LoadFactor loadFactor,
        Func<Command, CancellationToken, Task<CommandResponse>> handler)
    {
        if (AllSubscriptions.ContainsKey(subscriptionId)) return default;
        
        var subscription = new Subscription(
            commandHandlerId,
            subscriptionId,
            command,
            loadFactor,
            handler
        );

        AllSubscriptions.Add(subscriptionId, subscription);

        foreach (var otherSubscriptionId in SubscribeInstructions.Values)
        {
            if (AllSubscriptions.TryGetValue(otherSubscriptionId, out var otherSubscription) && otherSubscription.Command.Equals(command))
            {
                SupersededSubscriptions.Add(otherSubscriptionId);
            }
        }
            
        var instructionId = InstructionId.New();
        SubscribeInstructions.Add(instructionId, subscriptionId);
        return instructionId;

        // if (Active.TryGetValue(command, out var supersededSubscriptionId) && All.TryGetValue(supersededSubscriptionId, out var supersededSubscription))
        // {
        //         
        //     if (supersededSubscription.State is SubscriptionState.Subscribing or SubscriptionState.Unsubscribing)
        //     {
        //         Superseded.Add(supersededSubscriptionId);
        //     }
        //
        //     if (supersededSubscription.State is not SubscriptionState.Unsubscribing)
        //     {
        //         return default;
        //     }
        // }
    }

    public void Acknowledge(InstructionAck acknowledgement)
    {
        var instructionId = new InstructionId(acknowledgement.InstructionId);
            
        if (SubscribeInstructions.TryGetValue(instructionId, out var subscribeSubscriptionId)
            && AllSubscriptions.TryGetValue(subscribeSubscriptionId, out var subscribeSubscription))
        {
            SubscribeInstructions.Remove(instructionId);
                
            if (CompletionSources.TryGetValue(subscribeSubscription.CommandHandlerId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        CompletionSources.Remove(subscribeSubscription.CommandHandlerId);
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
                        CompletionSources.Remove(subscribeSubscription.CommandHandlerId);
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
                if (acknowledgement.Success)
                {
                    if (ActiveSubscriptions.TryGetValue(subscribeSubscription.Command, out var activeSubscriptionId))
                    {
                        ActiveSubscriptions[subscribeSubscription.Command] = subscribeSubscription.SubscriptionId;
                        ActiveHandlers[subscribeSubscription.Command] = subscribeSubscription.Handler;
                        AllSubscriptions.Remove(activeSubscriptionId);
                    }
                    else
                    {
                        ActiveSubscriptions.Add(subscribeSubscription.Command, subscribeSubscriptionId);
                        ActiveHandlers.Add(subscribeSubscription.Command, subscribeSubscription.Handler);
                    }
                }
            }
        }

            //All[subscribeSubscriptionId] = subscribeSubscription with { State = SubscriptionState.Subscribed };
        // }
        // else if (UnsubscribingByInstruction.TryGetValue(instructionId, out var unsubscribeSubscriptionId)
        //          && AllSubscriptions.TryGetValue(unsubscribeSubscriptionId, out var unsubscribeSubscription))
        // {
        //     UnsubscribingByInstruction.Remove(instructionId);
        //         
        //     AllSubscriptions.Remove(unsubscribeSubscriptionId);
        //     if (ActiveSubscriptions.TryGetValue(unsubscribeSubscription.Command, out var activeSubscriptionId) &&
        //         activeSubscriptionId == unsubscribeSubscriptionId)
        //     {
        //         ActiveSubscriptions.Remove(unsubscribeSubscription.Command);
        //         ActiveHandlers.Remove(unsubscribeSubscription.Command);
        //     }
        // }
    }

    public InstructionId? UnsubscribeFromCommand(
        SubscriptionId subscriptionId,
        CommandName command)
    {
        if (AllSubscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            AllSubscriptions.Remove(subscriptionId);
            if (ActiveSubscriptions.TryGetValue(subscription.Command, out var activeSubscriptionId) &&
                activeSubscriptionId == subscriptionId)
            {
                var instructionId = InstructionId.New();
                UnsubscribingByInstruction.Add(instructionId, subscriptionId);
                return instructionId;
            }
        }

        return default;
    }
}

// public class Commands
// {
//     private readonly Permits _permits;
//     private readonly Func<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> _handlers;
//
//     public Commands(Permits permits, Func<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> handlers)
//     {
//         _permits = permits;
//         _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
//     }
//     
//     public void 
// }