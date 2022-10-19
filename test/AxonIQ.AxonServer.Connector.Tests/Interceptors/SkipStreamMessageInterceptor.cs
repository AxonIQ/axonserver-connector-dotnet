using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

public class SkipStreamMessageInterceptor<TRequestMessage, TResponseMessage> : Interceptor
{
    private readonly Predicate<TRequestMessage> _skipRequestStreamMessage;
    private readonly Predicate<TResponseMessage> _skipResponseStreamMessage;

    public SkipStreamMessageInterceptor(Predicate<TRequestMessage> skipRequestStreamMessage, Predicate<TResponseMessage> skipResponseStreamMessage)
    {
        _skipRequestStreamMessage = skipRequestStreamMessage ?? throw new ArgumentNullException(nameof(skipRequestStreamMessage));
        _skipResponseStreamMessage = skipResponseStreamMessage ?? throw new ArgumentNullException(nameof(skipResponseStreamMessage));
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(context);
        var requestStream =
            typeof(TRequest) == typeof(TRequestMessage)
                ? new SkipWriteToClientAsyncStreamWriter<TRequest>(call.RequestStream,
                    message => !ReferenceEquals(message, null) && _skipRequestStreamMessage((TRequestMessage)message))
                : call.RequestStream;
        return new AsyncClientStreamingCall<TRequest, TResponse>(
            requestStream,
            call.ResponseAsync,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose
        );
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        var responseStream =
                typeof(TResponse) == typeof(TResponseMessage)
                    ? new SkipReadFromAsyncStreamReader<TResponse>(call.ResponseStream,
                        message => !ReferenceEquals(message, null) && _skipResponseStreamMessage((TResponseMessage)message))
                    : call.ResponseStream;
        return new AsyncServerStreamingCall<TResponse>(
            responseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose
        );
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(context);
        var requestStream =
            typeof(TRequest) == typeof(TRequestMessage)
                ? new SkipWriteToClientAsyncStreamWriter<TRequest>(call.RequestStream,
                    message => !ReferenceEquals(message, null) && _skipRequestStreamMessage((TRequestMessage)message))
                : call.RequestStream;
        var responseStream =
            typeof(TResponse) == typeof(TResponseMessage)
                ? new SkipReadFromAsyncStreamReader<TResponse>(call.ResponseStream,
                    message => !ReferenceEquals(message, null) && _skipResponseStreamMessage((TResponseMessage)message))
                : call.ResponseStream;
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            requestStream,
            responseStream,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);    
    }
}