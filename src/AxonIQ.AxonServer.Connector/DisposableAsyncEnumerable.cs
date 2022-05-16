namespace AxonIQ.AxonServer.Connector;

internal class DisposableAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<T> _enumerable;
    private readonly IDisposable _disposable;

    public DisposableAsyncEnumerable(IAsyncEnumerable<T> enumerable, IDisposable disposable)
    {
        _enumerable = enumerable;
        _disposable = disposable;
    }
    
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new DisposableAsyncEnumerator(_enumerable.GetAsyncEnumerator(cancellationToken), _disposable);
    }

    private class DisposableAsyncEnumerator : IAsyncEnumerator<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator;
        private readonly IDisposable _disposable;

        public DisposableAsyncEnumerator(IAsyncEnumerator<T> enumerator, IDisposable disposable)
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
            _disposable.Dispose();
        }
    }
}