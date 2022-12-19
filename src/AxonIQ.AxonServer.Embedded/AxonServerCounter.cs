namespace AxonIQ.AxonServer.Embedded;

public static class AxonServerCounter
{
    private static int _current = -1;

    public static int Next()
    {
        return Interlocked.Increment(ref _current);
    } 
}