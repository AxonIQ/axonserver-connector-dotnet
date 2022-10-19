using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class ControlledAvailabilityAsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse>
{
    private readonly IAsyncStreamReader<TResponse> _reader;
    private readonly Func<bool> _available;

    public ControlledAvailabilityAsyncStreamReader(IAsyncStreamReader<TResponse> reader, Func<bool> available)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _available = available ?? throw new ArgumentNullException(nameof(available));
    }
    
    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        if (!_available()) throw new RpcException(new Status(StatusCode.Unavailable, ""));
        return _reader.MoveNext(cancellationToken);
    }

    public TResponse Current => !_available() ? throw new RpcException(new Status(StatusCode.Unavailable, "")) : _reader.Current;
}