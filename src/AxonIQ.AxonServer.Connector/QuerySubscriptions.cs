using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

public class QuerySubscriptions
{
    public record Subscription(
        RegistrationId QueryHandlerId,
        RegistrationId SubscriptionId,
        QueryDefinition Query);

    public record QueryHandler(
        RegistrationId QueryHandlerId,
        IQueryHandler Handler);
    
    public QuerySubscriptions(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
        
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    
    public Dictionary<RegistrationId, QueryHandler> AllQueryHandlers { get; } = new();
    public Dictionary<RegistrationId, Subscription> AllSubscriptions { get; } = new();
    public Dictionary<RegistrationId, CountdownCompletionSource> SubscribeCompletionSources { get; } = new();
    public Dictionary<RegistrationId, CountdownCompletionSource> UnsubscribeCompletionSources { get; } = new();
    public Dictionary<QueryName, HashSet<RegistrationId>> ActiveSubscriptions { get; } = new();
    public Dictionary<QueryName, HashSet<IQueryHandler>> ActiveHandlers { get; } = new();
    public Dictionary<InstructionId, RegistrationId> SubscribeInstructions { get; } = new();
    public Dictionary<InstructionId, RegistrationId>  UnsubscribeInstructions { get; } = new();
    
    public void RegisterQueryHandler(
        RegistrationId queryHandlerId,
        CountdownCompletionSource subscribeCompletionSource,
        IQueryHandler handler)
    {
        if (!AllQueryHandlers.ContainsKey(queryHandlerId))
        {
            if (!SubscribeCompletionSources.ContainsKey(queryHandlerId))
            {
                SubscribeCompletionSources.Add(queryHandlerId, subscribeCompletionSource);
            }
            
            AllQueryHandlers.Add(queryHandlerId, new QueryHandler(queryHandlerId, handler));
        }
    }
    
    public InstructionId SubscribeToQuery(
        RegistrationId subscriptionId,
        RegistrationId queryHandlerId,
        QueryDefinition query)
    {
        if (!AllSubscriptions.ContainsKey(subscriptionId))
        {
            AllSubscriptions.Add(subscriptionId, new Subscription(
                queryHandlerId,
                subscriptionId,
                query
            ));
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
                
            if (SubscribeCompletionSources.TryGetValue(subscribeSubscription.QueryHandlerId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        SubscribeCompletionSources.Remove(subscribeSubscription.QueryHandlerId);
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
                        SubscribeCompletionSources.Remove(subscribeSubscription.QueryHandlerId);
                    }
                }
            }

            
            if (acknowledgement.Success && AllQueryHandlers.TryGetValue(subscribeSubscription.QueryHandlerId, out var queryHandler))
            {
                if (ActiveSubscriptions.TryGetValue(subscribeSubscription.Query.QueryName,
                        out var activeSubscriptionIds)
                    && ActiveHandlers.TryGetValue(subscribeSubscription.Query.QueryName, 
                        out var activeHandlers))
                {
                    activeSubscriptionIds.Add(subscribeSubscription.SubscriptionId);
                    activeHandlers.Add(queryHandler.Handler);
                    AllSubscriptions.Remove(subscribeSubscription.SubscriptionId);
                }
                else
                {
                    ActiveSubscriptions.Add(subscribeSubscription.Query.QueryName,
                        new HashSet<RegistrationId>(new[] { subscribeSubscriptionId }));
                    ActiveHandlers.Add(subscribeSubscription.Query.QueryName,
                        new HashSet<IQueryHandler>(new[] { queryHandler.Handler }));
                }
            }
        }
        else if (UnsubscribeInstructions.TryGetValue(instructionId, out var unsubscribeSubscriptionId)
                 && AllSubscriptions.TryGetValue(unsubscribeSubscriptionId, out var unsubscribeSubscription))
        {
            UnsubscribeInstructions.Remove(instructionId);
            AllSubscriptions.Remove(unsubscribeSubscriptionId);
            
            if (UnsubscribeCompletionSources.TryGetValue(unsubscribeSubscription.QueryHandlerId, out var completionSource))
            {
                if (acknowledgement.Success)
                {
                    if (completionSource.SignalSuccess())
                    {
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.QueryHandlerId);
                        AllQueryHandlers.Remove(unsubscribeSubscription.QueryHandlerId);
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
                        UnsubscribeCompletionSources.Remove(unsubscribeSubscription.QueryHandlerId);
                        AllQueryHandlers.Remove(unsubscribeSubscription.QueryHandlerId);
                    }
                }
            }
        }
    }
    
    public void UnregisterQueryHandler(
        RegistrationId queryHandlerId,
        CountdownCompletionSource unsubscribeCompletionSource)
    {
        if (AllQueryHandlers.ContainsKey(queryHandlerId))
        {
            if (!UnsubscribeCompletionSources.ContainsKey(queryHandlerId))
            {
                UnsubscribeCompletionSources.Add(queryHandlerId, unsubscribeCompletionSource);
            }
            
            //AllQueryHandlers.Remove(queryHandlerId);
        }
    }
    
    public InstructionId? UnsubscribeFromQuery(RegistrationId subscriptionId)
    {
        if (AllSubscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            if (ActiveSubscriptions.TryGetValue(subscription.Query.QueryName, out var activeSubscriptionIds) &&
                activeSubscriptionIds.Contains(subscriptionId) &&
                ActiveHandlers.TryGetValue(subscription.Query.QueryName, out var activeHandlers) &&
                AllQueryHandlers.TryGetValue(subscription.QueryHandlerId, out var queryHandler))
            {
                activeSubscriptionIds.Remove(subscriptionId);
                activeHandlers.Remove(queryHandler.Handler);
                
                var instructionId = InstructionId.New();
                UnsubscribeInstructions.Add(instructionId, subscriptionId);
                return instructionId;
            }
        }

        return default;
    }
}