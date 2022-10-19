using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class ConditionalAvailabilityClientAsyncStreamWriter<TRequest> : IClientStreamWriter<TRequest>
{
    private readonly IClientStreamWriter<TRequest> _writer;
    private readonly Predicate<object?> _available;

    public ConditionalAvailabilityClientAsyncStreamWriter(IClientStreamWriter<TRequest> writer, Predicate<object?> available)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _available = available ?? throw new ArgumentNullException(nameof(available));
    }
    public Task WriteAsync(TRequest message)
    {
        if (!_available(message))
        {
            throw new RpcException(new Status(StatusCode.Unavailable, ""));
        }
        return _writer.WriteAsync(message);
    }

    public WriteOptions? WriteOptions
    {
        get => _writer.WriteOptions;
        set => _writer.WriteOptions = value;
    }
    
    public Task CompleteAsync()
    {
        return _writer.CompleteAsync();
    }
}