using shortid;
using shortid.Configuration;

namespace AxonIQ.AxonServer.Embedded;

public static class ToxiProxyCounter
{
    private static readonly string Prefix = ShortId.Generate(new GenerationOptions
    (
        true,
        false,
        8
    ));
    
    private static int _current = -1;

    public static string Next()
    {
        return $"T{Prefix}-{Interlocked.Increment(ref _current)}";
    } 
}