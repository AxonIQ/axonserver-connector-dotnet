namespace AxonIQ.AxonServer.Connector.Tests;

public class ConcurrentFlowControlTests
{
    private readonly ConcurrentFlowControl _sut = new();

    [Fact]
    public void IsFlowControl()
    {
        Assert.IsAssignableFrom<IFlowControl>(_sut);
    }

    [Fact]
    public void TryTakeHasExpectedResultWhenNoneRequested()
    {
        Assert.False(_sut.TryTake());
    }
    
    [Fact]
    public void TryTakeHasExpectedResultWhenZeroRequested()
    {
        _sut.Request(0);
        Assert.False(_sut.TryTake());
    }
    
    [Fact]
    public void TryTakeHasExpectedResultWhenOneRequested()
    {
        _sut.Request(1);
        Assert.True(_sut.TryTake());
        Assert.False(_sut.TryTake());
    }

    [Fact]
    public void TryTakeHasExpectedResultWhenCancelled()
    {
        _sut.Request(1);
        _sut.Cancel();
        Assert.False(_sut.TryTake());
    }
    
    [Fact]
    public Task WaitToTakeAsyncHasExpectedResultWhenNoneRequested()
    {
        return Assert.ThrowsAsync<TimeoutException>(async () => await _sut.WaitToTakeAsync().WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
    
    [Fact]
    public Task WaitToTakeAsyncHasExpectedResultWhenZeroRequested()
    {
        _sut.Request(0);
        return Assert.ThrowsAsync<TimeoutException>(async () => await _sut.WaitToTakeAsync().WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
    
    [Fact]
    public async Task WaitToTakeAsyncHasExpectedResultWhenOneRequested()
    {
        _sut.Request(1);
        Assert.True(await _sut.WaitToTakeAsync().WaitAsync(TimeSpan.FromMilliseconds(5)));
        Assert.True(_sut.TryTake());
        await Assert.ThrowsAsync<TimeoutException>(async () => await _sut.WaitToTakeAsync().WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
    
    [Fact]
    public async Task WaitToTakeAsyncHasExpectedResultWhenCancelled()
    {
        _sut.Request(1);
        _sut.Cancel();
        Assert.False(await _sut.WaitToTakeAsync().WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
    
    [Fact]
    public async Task WaitToTakeAsyncHasExpectedResultWhenOneRequestedAfterWait()
    {
        var wait= _sut.WaitToTakeAsync();
        Assert.False(wait.IsCompleted);
        _sut.Request(1);
        Assert.True(await wait.WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
    
    [Fact]
    public async Task WaitToTakeAsyncHasExpectedResultWhenCancelledAfterWait()
    {
        var wait= _sut.WaitToTakeAsync();
        Assert.False(wait.IsCompleted);
        _sut.Cancel();
        Assert.False(await wait.WaitAsync(TimeSpan.FromMilliseconds(5)));
    }
}