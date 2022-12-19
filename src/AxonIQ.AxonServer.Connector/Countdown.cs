namespace AxonIQ.AxonServer.Connector;

public class Countdown
{

    public Countdown(int initialCount)
    {
        InitialCount = initialCount;
        CurrentCount = initialCount;
    }
    
    public int InitialCount { get; }
    public int CurrentCount { get; private set; }

    public bool Signal()
    {
        CurrentCount -= 1;
        return CurrentCount == 0;
    }

    public void Reset()
    {
        CurrentCount = InitialCount;
    }
}

public class ConcurrentCountdown
{
    private int _currentCount;
    
    public ConcurrentCountdown(int initialCount)
    {
        InitialCount = initialCount;
        _currentCount = initialCount;
    }
    
    public int InitialCount { get; }
    public int CurrentCount => _currentCount;

    public bool Signal()
    {
        var count = Interlocked.Decrement(ref _currentCount);
        return count == 0;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _currentCount, InitialCount);
    }
}