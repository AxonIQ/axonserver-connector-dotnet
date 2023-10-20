using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

internal static class ScheduleDue
{
    public static TimeSpan FromException(Exception exception)
    {
        var statusCode =
            exception switch
            {
                RpcException rpc => rpc.StatusCode,
                IOException => StatusCode.Unknown,
                _ => StatusCode.Unknown
            };

        return FromStatusCode(statusCode);
    }
    
    public static TimeSpan FromStatusCode(StatusCode code)
    {
        var after = TimeSpan.FromMilliseconds(500);
        switch (code)
        {
            case StatusCode.NotFound:
            case StatusCode.PermissionDenied:
            case StatusCode.Unimplemented:
            case StatusCode.Unauthenticated:
            case StatusCode.FailedPrecondition:
            case StatusCode.InvalidArgument:
            case StatusCode.ResourceExhausted:
                after = TimeSpan.FromMilliseconds(5000);
                break;
            case StatusCode.Unavailable:
                after = TimeSpan.FromMilliseconds(50);
                break;
        }

        return after;
    }
}