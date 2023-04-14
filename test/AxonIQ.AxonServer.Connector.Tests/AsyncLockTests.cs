using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AsyncLockTests
{
    [Fact]
    public async Task AcquireAsyncHoldsOntoLockBeforeAnotherAcquireCanHappen()
    {
        var source = new CancellationTokenSource();
        await using var sut = new AsyncLock();
        var locked1 = await sut.AcquireAsync(source.Token);
        var acquiring = sut.AcquireAsync(source.Token);
        Assert.False(acquiring.IsCompleted);
        locked1.Dispose();
        var locked2 = await acquiring;
        Assert.True(acquiring.IsCompleted);
        locked2.Dispose();
    }
    
    [Fact]
    public async Task AcquireAsyncThrowsWhenTokenIsCancelled()
    {
        var source = new CancellationTokenSource();
        source.Cancel();
        
        var sut = new AsyncLock();
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.AcquireAsync(source.Token));
    }

    [Fact]
    public async Task AcquireAsyncThrowsWhenLockIsDisposed()
    {
        var source = new CancellationTokenSource();
        var sut = new AsyncLock();
        await sut.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.AcquireAsync(source.Token));
    }

    [Fact]
    public async Task RaceBetweenAcquireAsyncAndDisposeAsyncHasExpectedResult()
    {
        var source = new CancellationTokenSource();
        var sut = new AsyncLock();

        if (Random.Shared.Next() % 2 == 0)
        {
            try
            {
                var acquire = sut.AcquireAsync(source.Token);
                var dispose = sut.DisposeAsync();
                using(await acquire) {}
                await dispose;
            } catch(ObjectDisposedException) {}
            
        } else{
            try
            {
                var dispose = sut.DisposeAsync();
                var acquire = sut.AcquireAsync(source.Token);
                using(await acquire) {}
                await dispose;
            } catch(ObjectDisposedException) {}
            
        }
        
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.AcquireAsync(source.Token));
    }
}