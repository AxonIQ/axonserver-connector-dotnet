using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class SkipReadFromAsyncStreamReader<TResponse> : IAsyncStreamReader<TResponse>
{
    private readonly IAsyncStreamReader<TResponse> _reader;
    private readonly Predicate<object?> _skip;

    public SkipReadFromAsyncStreamReader(IAsyncStreamReader<TResponse> reader, Predicate<object?> skip)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _skip = skip ?? throw new ArgumentNullException(nameof(skip));
    }
        
    public async Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        var moved = await _reader.MoveNext(cancellationToken);
        while (moved && _skip(_reader.Current))
        {
            moved = await _reader.MoveNext(cancellationToken);
        }
        return moved;
    }

    public TResponse Current => _reader.Current;
}