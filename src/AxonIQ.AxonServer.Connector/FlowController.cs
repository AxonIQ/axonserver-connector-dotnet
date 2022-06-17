namespace AxonIQ.AxonServer.Connector;

public class FlowController
{
    private PermitCounter _current;
    
    public FlowController(PermitCount threshold)
    {
        Threshold = threshold;
        _current = PermitCounter.Zero;
    }
    
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