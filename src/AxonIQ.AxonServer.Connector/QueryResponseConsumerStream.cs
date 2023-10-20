using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class QueryResponseConsumerStream : IAsyncEnumerable<QueryResponse>
{
    private readonly IAsyncEnumerable<QueryResponse> _enumerable;
    private readonly IDisposable _disposable;

    public QueryResponseConsumerStream(IAsyncEnumerable<QueryResponse> enumerable, IDisposable disposable)
    {
        _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
    }
    
    public IAsyncEnumerator<QueryResponse> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new DisposableAsyncEnumerator(_enumerable.GetAsyncEnumerator(cancellationToken), _disposable);
    }

    private class DisposableAsyncEnumerator : IAsyncEnumerator<QueryResponse>
    {
        private readonly IAsyncEnumerator<QueryResponse> _enumerator;
        private readonly IDisposable _disposable;
        
        public DisposableAsyncEnumerator(IAsyncEnumerator<QueryResponse> enumerator, IDisposable disposable)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync();
        }

        public QueryResponse Current => _enumerator.Current;

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync().ConfigureAwait(false);
            _disposable.Dispose();
        }
    }
}