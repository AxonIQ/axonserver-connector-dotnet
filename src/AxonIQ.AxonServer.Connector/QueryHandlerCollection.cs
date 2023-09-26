using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class QueryHandlerCollection
{
    private record RegisteredQuery(RegisteredQueryId Id, QueryDefinition Definition, IQueryHandler Handler);
    private record SubscribingToQuery(QueryDefinition Query, DateTimeOffset Since);
    private record UnsubscribingFromQuery(QueryDefinition Query, DateTimeOffset Since);
    
    private readonly Dictionary<RegisteredQueryId, RegisteredQuery> _registeredQueries = new();
    private readonly Dictionary<QueryName, HashSet<RegisteredQueryId>> _registeredQueryHandlers = new();
    private readonly Dictionary<QueryDefinition, int> _subscribedQueries = new();
    
    private readonly Dictionary<InstructionId, SubscribingToQuery> _subscribing = new();
    private readonly Dictionary<InstructionId, UnsubscribingFromQuery> _unsubscribing = new();
    
    private readonly Dictionary<QueryDefinition, List<TaskCompletionSource>> _subscribeCompletionSources = new();
    private readonly Dictionary<QueryDefinition, List<TaskCompletionSource>> _unsubscribeCompletionSources = new();
    
    public QueryHandlerCollection(ClientIdentity clientIdentity, Func<DateTimeOffset> clock)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }
    
    public ClientIdentity ClientIdentity { get; }
    public Func<DateTimeOffset> Clock { get; }
    
    public bool HasRegisteredQueries => _registeredQueries.Count != 0;
    public int RegisteredQueryCount => _registeredQueries.Count;

    public IReadOnlyCollection<IQueryHandler> ResolveQueryHandlers(QueryName name)
    {
        if (_registeredQueryHandlers.TryGetValue(name, out var queries))
        {
            return queries
                .Select(query => _registeredQueries[query].Handler)
                .ToArray();
        }
        return Array.Empty<IQueryHandler>();
    }

    public void RegisterQueryHandler(RegisteredQueryId id, QueryDefinition definition, IQueryHandler handler)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        if (!_registeredQueries.ContainsKey(id))
        {
            var registeredQuery = new RegisteredQuery(id, definition, handler);

            _registeredQueries.Add(id, registeredQuery);

            if (_registeredQueryHandlers.TryGetValue(registeredQuery.Definition.QueryName, out var handlers))
            {
                handlers.Add(registeredQuery.Id);
            }
            else
            {
                _registeredQueryHandlers.Add(
                    registeredQuery.Definition.QueryName,
                    new HashSet<RegisteredQueryId>(new[] { registeredQuery.Id }));
            }
        }
    }
    
    public void UnregisterQueryHandler(RegisteredQueryId id)
    {
        if (_registeredQueries.Remove(id, out var registeredQuery) 
            && _registeredQueryHandlers.TryGetValue(registeredQuery.Definition.QueryName, out var handlers))
        {
            handlers.Remove(registeredQuery.Id);
        }
    }
    
    private QueryProviderOutbound BeginSubscribeToQueryInstruction(QueryDefinition query)
    {
        var instructionId = InstructionId.New();
        _subscribing.Add(instructionId, new SubscribingToQuery(query, Clock()));
        var message = new QueryProviderOutbound
        {
            InstructionId = instructionId.ToString(),
            Subscribe = new QuerySubscription
            {
                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                ComponentName = ClientIdentity.ComponentName.ToString(),
                Query = query.QueryName.ToString(),
                ResultName = query.ResultName,
                MessageId = instructionId.ToString()
            }
        };
        return message;
    }
    
    private QueryProviderOutbound BeginUnsubscribeFromQueryInstruction(QueryDefinition query)
    {
        var instructionId = InstructionId.New();
        _unsubscribing.Add(instructionId, new UnsubscribingFromQuery(query, Clock()));
        var message = new QueryProviderOutbound
        {
            InstructionId = instructionId.ToString(),
            Unsubscribe = new QuerySubscription
            {
                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                ComponentName = ClientIdentity.ComponentName.ToString(),
                Query = query.QueryName.ToString(),
                ResultName = query.ResultName,
                MessageId = instructionId.ToString()
            }
        };
        return message;
    }
    
    public IReadOnlyCollection<QueryProviderOutbound> BeginSubscribeToAllInstructions()
    {
        var messages = new List<QueryProviderOutbound>();
        var definitions = new HashSet<QueryDefinition>();
        foreach(var (_, registeredQuery) in _registeredQueries)
        {
            if (!definitions.Contains(registeredQuery.Definition))
            {
                messages.Add(BeginSubscribeToQueryInstruction(registeredQuery.Definition));
            }

            definitions.Add(registeredQuery.Definition);
        }

        return messages;
    }
    
    public IReadOnlyCollection<QueryProviderOutbound> BeginUnsubscribeFromAllInstructions()
    {
        var messages = new List<QueryProviderOutbound>();
        var definitions = new HashSet<QueryDefinition>();
        foreach(var (_, registeredQuery) in _registeredQueries)
        {
            if (!definitions.Contains(registeredQuery.Definition))
            {
                messages.Add(BeginUnsubscribeFromQueryInstruction(registeredQuery.Definition));
            }

            definitions.Add(registeredQuery.Definition);
        }

        return messages;
    }

    public void RegisterSubscribeToQueryCompletionSource(QueryDefinition query,
        TaskCompletionSource completionSource)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (completionSource == null) throw new ArgumentNullException(nameof(completionSource));

        if (_subscribeCompletionSources.TryGetValue(query, out var completionSources))
        {
            completionSources.Add(completionSource);
        }
        else
        {
            _subscribeCompletionSources.Add(query, new List<TaskCompletionSource> { completionSource });
        }
    }
    
    public void RegisterUnsubscribeFromQueryCompletionSource(QueryDefinition query,
        TaskCompletionSource completionSource)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (completionSource == null) throw new ArgumentNullException(nameof(completionSource));

        if (_unsubscribeCompletionSources.TryGetValue(query, out var completionSources))
        {
            completionSources.Add(completionSource);
        }
        else
        {
            _unsubscribeCompletionSources.Add(query, new List<TaskCompletionSource> { completionSource });
        }
    }
    
    public QueryProviderOutbound? TryBeginSubscribeToQueryInstruction(QueryDefinition query)
    {
        if(_subscribedQueries.TryGetValue(query, out var count) && count > 0)
        {
            _subscribedQueries[query] = count + 1;
            return null;
        }

        var instruction = BeginSubscribeToQueryInstruction(query);
        _subscribedQueries[query] = 1;
        return instruction;
    }
    
    public QueryProviderOutbound? TryBeginUnsubscribeFromQueryInstruction(QueryDefinition query)
    {
        if(_subscribedQueries.TryGetValue(query, out var count))
        {
            if (count == 1)
            {
                var instruction = BeginUnsubscribeFromQueryInstruction(query);
                _subscribedQueries.Remove(query);
                return instruction;
            }
            
            _subscribedQueries[query] = count - 1;
        }

        return null;
    }
    
    
    public bool TryCompleteSubscribeToQueryInstruction(InstructionAck acknowledgement)
    {
        if (acknowledgement == null) throw new ArgumentNullException(nameof(acknowledgement));
        
        if (InstructionId.TryParse(acknowledgement.InstructionId, out var id))
        {
            if (!_subscribing.Remove(id, out var subscribe))
            {
                return false;
            }

            if (_subscribeCompletionSources.Remove(subscribe.Query, out var completionSources))
            {
                if (acknowledgement.Success)
                {
                    foreach (var completionSource in completionSources)
                    {
                        completionSource.TrySetResult();
                    }
                }
                else
                {
                    foreach (var completionSource in completionSources)
                    {
                        completionSource.TrySetException(
                            AxonServerException.FromErrorMessage(ClientIdentity, acknowledgement.Error));
                    }
                }
            }

            return true;
        }

        return false;
    }
    
    public bool TryCompleteUnsubscribeFromQueryInstruction(InstructionAck acknowledgement)
    {
        if (acknowledgement == null) throw new ArgumentNullException(nameof(acknowledgement));
        
        if (InstructionId.TryParse(acknowledgement.InstructionId, out var id))
        {
            if (!_unsubscribing.Remove(id, out var unsubscribe))
            {
                return false;
            }

            if (_unsubscribeCompletionSources.Remove(unsubscribe.Query, out var completionSources))
            {
                if (acknowledgement.Success)
                {
                    foreach (var completionSource in completionSources)
                    {
                        completionSource.TrySetResult();
                    }
                }
                else
                {
                    foreach (var completionSource in completionSources)
                    {
                        completionSource.TrySetException(
                            AxonServerException.FromErrorMessage(ClientIdentity, acknowledgement.Error));
                    }
                }
            }

            return true;
        }

        return false;
    }

    public void Purge(TimeSpan age)
    {
        var threshold = Clock().Subtract(age);

        var overdueSubscribeInstructions =
            _subscribing
                .Where(item => item.Value.Since <= threshold)
                .ToArray();
        foreach (var (instruction, subscribe) in overdueSubscribeInstructions)
        {
            _subscribing.Remove(instruction);

            if (_subscribeCompletionSources.Remove(subscribe.Query, out var completionSources))
            {
                foreach (var completionSource in completionSources)
                {
                    completionSource.TrySetException(
                        new AxonServerException(
                            ClientIdentity, ErrorCategory.Other,
                            $"The subscribe instruction with identifier {instruction.ToString()} was not acknowledged in time."));
                }
            }
        }
        
        var overdueUnsubscribeInstructions =
            _unsubscribing
                .Where(item => item.Value.Since <= threshold)
                .ToArray();
        foreach (var (instruction, unsubscribe) in overdueUnsubscribeInstructions)
        {
            _unsubscribing.Remove(instruction);

            if (_unsubscribeCompletionSources.Remove(unsubscribe.Query, out var completionSources))
            {
                foreach (var completionSource in completionSources)
                {
                    completionSource.TrySetException(
                        new AxonServerException(
                            ClientIdentity, ErrorCategory.Other,
                            $"The unsubscribe instruction with identifier {instruction.ToString()} was not acknowledged in time."));
                }
            }
        }
    }

}