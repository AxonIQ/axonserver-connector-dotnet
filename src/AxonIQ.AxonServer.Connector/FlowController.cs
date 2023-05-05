namespace AxonIQ.AxonServer.Connector;

internal class FlowController
{
    private PermitCounter _current;
    
    public FlowController(PermitCount initial, PermitCount threshold)
    {
        Initial = initial;
        Threshold = threshold;
        _current = PermitCounter.Zero;
    }

    public PermitCount Initial { get; }
    public PermitCount Threshold { get; }

    public bool Increment()
    {
        _current = _current.Increment();
        if (Threshold != _current) return false;
        _current = PermitCounter.Zero;
        return true;
    }

    public void Reset()
    {
        _current = PermitCounter.Zero;
    }
}