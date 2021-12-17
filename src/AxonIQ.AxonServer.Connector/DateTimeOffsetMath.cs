namespace AxonIQ.AxonServer.Connector;

public static class DateTimeOffsetMath
{
    public static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Math.Min(left.ToUnixTimeMilliseconds(), right.ToUnixTimeMilliseconds()));
    }
    
    public static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(left.ToUnixTimeMilliseconds(), right.ToUnixTimeMilliseconds()));
    }
}