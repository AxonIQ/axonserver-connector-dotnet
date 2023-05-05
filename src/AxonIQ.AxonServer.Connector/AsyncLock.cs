namespace AxonIQ.AxonServer.Connector;

internal class  AsyncLock : IAsyncDisposable
{
    private long _disposed;
    
    private readonly SemaphoreSlim _semaphore;
    private readonly IDisposable _releaser;
    private readonly Task<IDisposable> _release;
    
    public AsyncLock()
    {
        _semaphore  = new SemaphoreSlim(1, 1);
        _releaser = new LockReleaser(_semaphore);
        _release = Task.FromResult(_releaser);
        _disposed = 0L;
    }

    public Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        
        var wait = _semaphore.WaitAsync(ct);
        return wait.IsCompleted
            ? _release
            : wait.ContinueWith((_, _) => _releaser, null, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes) throw new ObjectDisposedException(nameof(AsyncLock));
    }
    
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            await _semaphore.WaitAsync();
            _semaphore.Dispose();
        }
    }

    private class LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }
        
        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}