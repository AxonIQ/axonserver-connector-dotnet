namespace AxonIQ.AxonServer.Connector;

public static class TimeSpanMath
{
    public static TimeSpan Min(TimeSpan left, TimeSpan right)
    {
        return TimeSpan.FromTicks(Math.Min(left.Ticks, right.Ticks));
    }
    
    public static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return TimeSpan.FromTicks(Math.Min(left.Ticks, right.Ticks));
    }
}