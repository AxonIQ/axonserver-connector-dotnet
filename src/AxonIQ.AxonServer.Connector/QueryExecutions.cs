namespace AxonIQ.AxonServer.Connector;

internal class QueryExecutions
{
    private readonly Dictionary<InstructionId, QueryExecution> _executions = new();

    public void Add(InstructionId query, QueryExecution execution)
    {
        _executions.Add(query, execution);
    }
    
    public bool TryGet(InstructionId query, out QueryExecution? execution)
    {
        return _executions.TryGetValue(query, out execution);
    }

    public bool TryRemove(InstructionId query, out QueryExecution? execution)
    {
        return _executions.Remove(query, out execution);
    }
}