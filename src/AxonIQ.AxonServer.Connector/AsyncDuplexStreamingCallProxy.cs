using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public class AsyncDuplexStreamingCallProxy<TRequest, TResponse>
{
    private readonly Func<AsyncDuplexStreamingCall<TRequest, TResponse>?> _factory;

    public AsyncDuplexStreamingCallProxy(Func<AsyncDuplexStreamingCall<TRequest, TResponse>?> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public AsyncDuplexStreamingCall<TRequest, TResponse> Call => _factory() ?? throw new NotSupportedException();
    
    public IAsyncStreamReader<TResponse> ResponseStream => Call.ResponseStream;

    public IClientStreamWriter<TRequest> RequestStream => Call.RequestStream;

    public Task<Metadata> ResponseHeadersAsync => Call.ResponseHeadersAsync;

    public Status GetStatus() => Call.GetStatus();

    public Metadata GetTrailers() => Call.GetTrailers();
}