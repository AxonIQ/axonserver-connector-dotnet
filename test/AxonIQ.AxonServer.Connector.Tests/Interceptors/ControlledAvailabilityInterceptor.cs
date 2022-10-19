using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

public class ControlledAvailabilityInterceptor : Interceptor
{
    public bool Available { get; set; } = true;

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var response = continuation(request, context);
        return Available ? response : throw new RpcException(new Status(StatusCode.Unavailable, ""));
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        var responseAsync = Task.Run(async () =>
            Available ? await call.ResponseAsync : throw new RpcException(new Status(StatusCode.Unavailable, ""))
        );
        return new AsyncUnaryCall<TResponse>(
            responseAsync,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(context);
        var responseAsync = Task.Run(async () =>
            Available ? await call.ResponseAsync : throw new RpcException(new Status(StatusCode.Unavailable, ""))
        );
        return new AsyncClientStreamingCall<TRequest, TResponse>(
            new ControlledAvailabilityClientAsyncStreamWriter<TRequest>(call.RequestStream, () => Available),
            responseAsync,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        return new AsyncServerStreamingCall<TResponse>(
            new ControlledAvailabilityAsyncStreamReader<TResponse>(call.ResponseStream, () => Available),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(context);
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            new ControlledAvailabilityClientAsyncStreamWriter<TRequest>(call.RequestStream, () => Available),
            new ControlledAvailabilityAsyncStreamReader<TResponse>(call.ResponseStream, () => Available),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }
}