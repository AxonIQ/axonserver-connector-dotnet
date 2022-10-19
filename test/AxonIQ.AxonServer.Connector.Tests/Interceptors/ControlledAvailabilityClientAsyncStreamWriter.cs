using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class ControlledAvailabilityClientAsyncStreamWriter<TRequest> : IClientStreamWriter<TRequest>
{
    private readonly IClientStreamWriter<TRequest> _writer;
    private readonly Func<bool> _available;

    public ControlledAvailabilityClientAsyncStreamWriter(IClientStreamWriter<TRequest> writer, Func<bool> available)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _available = available ?? throw new ArgumentNullException(nameof(available));
    }

    public Task WriteAsync(TRequest message)
    {
        if (!_available()) throw new RpcException(new Status(StatusCode.Unavailable, ""));
        return _writer.WriteAsync(message);
    }

    public WriteOptions? WriteOptions
    {
        get => _writer.WriteOptions;
        set => _writer.WriteOptions = value;
    }
    
    public Task CompleteAsync()
    {
        if (!_available()) throw new RpcException(new Status(StatusCode.Unavailable, ""));
        return _writer.CompleteAsync();
    }
}