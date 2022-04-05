namespace AxonIQ.AxonServer.Connector;

public class FlowController
{
    private readonly PermitCount _threshold;
    
    private PermitCounter _current;
    
    public FlowController(PermitCount threshold)
    {
        _threshold = threshold;
        _current = PermitCounter.Zero;
    }

    public bool Increment()
    {
        _current = _current.Increment();
        if (_threshold != _current) return false;
        _current = PermitCounter.Zero;
        return true;
    }

    public void Reset()
    {
        _current = PermitCounter.Zero;
    }
}