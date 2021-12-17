using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public class FaultyCallInvoker : CallInvoker
{
    private readonly Status _status;
    private readonly Metadata _trailers;
    private readonly string _message;

    public FaultyCallInvoker(Status status, Metadata trailers, string message)
    {
        _status = status;
        _trailers = trailers;
        _message = message;
    }
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
    {
        throw new RpcException(_status, _trailers, _message); 
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
    {
        throw new RpcException(_status, _trailers, _message);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options,
        TRequest request)
    {
        throw new RpcException(_status, _trailers, _message);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
    {
        throw new RpcException(_status, _trailers, _message);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
    {
        throw new RpcException(_status, _trailers, _message);
    }
}