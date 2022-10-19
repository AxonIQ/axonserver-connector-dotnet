using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class ConditionalAvailabilityAsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse>
{
    private readonly IAsyncStreamReader<TResponse> _reader;
    private readonly Predicate<object?> _available;

    public ConditionalAvailabilityAsyncStreamReader(IAsyncStreamReader<TResponse> reader, Predicate<object?> available)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _available = available ?? throw new ArgumentNullException(nameof(available));
    }
        
    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        var moved = await _reader.MoveNext(cancellationToken);
        if (moved && !_available(_reader.Current))
        {
            throw new RpcException(new Status(StatusCode.Unavailable, ""));
        }
        return moved;
    }

    public TResponse Current => !_available(_reader.Current) ? throw new RpcException(new Status(StatusCode.Unavailable, "")) : _reader.Current;
}