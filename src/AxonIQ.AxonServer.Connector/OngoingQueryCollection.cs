namespace AxonIQ.AxonServer.Connector;

public class OngoingQueryCollection
{
    private readonly Dictionary<InstructionId, IAsyncDisposable> _queries;

    public OngoingQueryCollection()
    {
        _queries = new Dictionary<InstructionId, IAsyncDisposable>();
    }
    
    public void AddQuery(InstructionId queryId, IAsyncDisposable forwarder)
    {
        _queries.Add(queryId, forwarder);
    }

    public bool TryFlowControlRequestForQuery(InstructionId queryId, long requested)
    {
        if (_queries.TryGetValue(queryId, out var forwarder) && forwarder is IFlowControl flowControlled)
        {
            flowControlled.Request(requested);
            return true;
        }
        
        return false;
    }

    public IAsyncDisposable? RemoveQuery(InstructionId queryId)
    {
        return _queries.Remove(queryId, out var forwarder) ? forwarder : null;
    }
}