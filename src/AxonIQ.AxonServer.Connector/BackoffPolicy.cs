namespace AxonIQ.AxonServer.Connector;

public class BackoffPolicy
{
    private TimeSpan? _current;

    public BackoffPolicy(BackoffPolicyOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }
    
    public BackoffPolicyOptions Options { get; }

    public TimeSpan Next()
    {
        var next = TimeSpan.Zero;
        if (!_current.HasValue)
        {
            next = Options.InitialBackoff;
        } 
        else if (_current.Value == Options.MaximumBackoff)
        {
            next = Options.MaximumBackoff;
        }
        else
        {
            if (Options.BackoffMultiplier <= 1.0)
            {
                next = _current.Value * Options.BackoffMultiplier;
            }
            else
            {
                next = _current.Value * Options.BackoffMultiplier;
                if (next > Options.MaximumBackoff)
                {
                    next = Options.MaximumBackoff;
                }
            }
        }

        _current = next;

        return next;
    }

    public void Reset()
    {
        _current = new TimeSpan?();
    }
}