using System.Threading.Tasks;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CooperativeCancellationTests
{
    [Fact]
    public async Task Test1()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new CooperativeCancellation();

        var stream = sut.OpenStream();

        var t = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream)
                {
                }
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(1));
        
        sut.Reconnect();

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await sut.DisposeAsync();
    }
    
    [Fact]
    public async Task Test2()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new CooperativeCancellation();

        var stream = sut.OpenStream();

        var t = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream)
                {
                }
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(1));

        await sut.DisposeAsync();
        
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}

public class CooperativeCancellation : IAsyncDisposable
{
    private CancellationTokenSource? _source;
    private long _disposed;

    public CooperativeCancellation()
    {
        _source = new CancellationTokenSource();
    }

    public void Reconnect()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.No)
        {
            var next = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _source, next);
            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }
            else
            {   // if we lost the race with DisposeAsync(), we must clean up here
                next.Cancel();
                next.Dispose();
            }
        }
        
        
    }

    public IAsyncEnumerable<object> OpenStream()
    {
        ThrowIfDisposed();
        return new Enumerable(_source?.Token ?? CancellationToken.None);
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes)
            throw new ObjectDisposedException(nameof(CooperativeCancellation));
    }
    
    private class Enumerable : IAsyncEnumerable<object>
    {
        private readonly CancellationToken _token;

        public Enumerable(CancellationToken token)
        {
            _token = token;
        }
        
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var source = cancellationToken == CancellationToken.None 
                ? CancellationTokenSource.CreateLinkedTokenSource(_token) 
                : CancellationTokenSource.CreateLinkedTokenSource(_token, cancellationToken);
            return new Enumerator(source);
        }
    
        private class Enumerator : IAsyncEnumerator<object>
        {
            private readonly CancellationTokenSource _source;

            public Enumerator(CancellationTokenSource source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }
        
            public ValueTask DisposeAsync()
            {
                _source.Dispose();
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                _source.Token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(true);
            }

            public object Current => new ();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            var source = Interlocked.Exchange(ref _source, null);
            if (source != null)
            {
                source.Cancel();
                source.Dispose();    
            }
        }

        return ValueTask.CompletedTask;
    }
}