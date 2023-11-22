namespace AxonIQ.AxonServer.Connector;

internal class SubscriptionQueryExecutions
{
    private readonly Dictionary<SubscriptionId, SubscriptionQueryExecution> _executions = new();

    public void Add(SubscriptionId query, SubscriptionQueryExecution execution)
    {
        _executions.Add(query, execution);
    }
    
    public bool TryGet(SubscriptionId query, out SubscriptionQueryExecution? execution)
    {
        return _executions.TryGetValue(query, out execution);
    }
    public bool TryRemove(SubscriptionId query, out SubscriptionQueryExecution? execution)
    {
        return _executions.Remove(query, out execution);
    }
}