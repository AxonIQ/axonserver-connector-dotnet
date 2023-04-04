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
            // case StatusCode.OK:
            //     break;
            // case StatusCode.Cancelled:
            //     break;
            // case StatusCode.Unknown:
            //     break;
            // case StatusCode.DeadlineExceeded:
            //     break;
            // case StatusCode.AlreadyExists:
            //     break;
            // case StatusCode.Aborted:
            //     break;
            // case StatusCode.OutOfRange:
            //     break;
            // case StatusCode.Internal:
            //     break;
            // case StatusCode.DataLoss:
            //     break;
        }

        return after;
    }
}