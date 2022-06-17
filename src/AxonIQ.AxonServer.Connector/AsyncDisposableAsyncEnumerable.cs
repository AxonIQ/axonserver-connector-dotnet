namespace AxonIQ.AxonServer.Connector;

internal class AsyncDisposableAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<T> _enumerable;
    private readonly IAsyncDisposable _disposable;

    public AsyncDisposableAsyncEnumerable(IAsyncEnumerable<T> enumerable, IAsyncDisposable disposable)
    {
        _enumerable = enumerable;
        _disposable = disposable;
    }
    
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new AsyncDisposableAsyncEnumerator(_enumerable.GetAsyncEnumerator(cancellationToken), _disposable);
    }

    private class AsyncDisposableAsyncEnumerator : IAsyncEnumerator<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator;
        private readonly IAsyncDisposable _disposable;

        public AsyncDisposableAsyncEnumerator(IAsyncEnumerator<T> enumerator, IAsyncDisposable disposable)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync();
        }

        public T Current => _enumerator.Current;

        public async ValueTask DisposeAsync()
        {
            await _enumerator.DisposeAsync();
            await _disposable.DisposeAsync();
        }
    }
}