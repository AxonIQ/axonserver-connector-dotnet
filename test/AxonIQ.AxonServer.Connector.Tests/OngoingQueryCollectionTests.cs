namespace AxonIQ.AxonServer.Connector.Tests;

public class OngoingQueryCollectionTests
{
    private readonly OngoingQueryCollection _sut;

    public OngoingQueryCollectionTests()
    {
        _sut = new OngoingQueryCollection();
    }
    
    [Fact]
    public void AddQueryHasExpectedResult()
    {
        var queryId = InstructionId.New();
        var forwarder = new FakeForwarder();
        _sut.AddQuery(queryId, forwarder);
        
        Assert.False(_sut.TryFlowControlRequestForQuery(queryId, 1));
        
        Assert.Same(forwarder, _sut.RemoveQuery(queryId));
        Assert.Null(_sut.RemoveQuery(queryId));
    }
    
    [Fact]
    public void TryFlowControlRequestForNonExistingQueryHasExpectedResult()
    {
        Assert.False(_sut.TryFlowControlRequestForQuery(InstructionId.New(), 1));
    }
    
    [Fact]
    public void TryFlowControlRequestForQueryHasExpectedResult()
    {
        var queryId = InstructionId.New();
        var forwarder = new FakeFlowControlledForwarder();
        _sut.AddQuery(queryId, forwarder);
        
        Assert.True(_sut.TryFlowControlRequestForQuery(queryId, 1));
        Assert.True(_sut.TryFlowControlRequestForQuery(queryId, 1));
        Assert.Equal(2, forwarder.Requested);
    }

    private class FakeForwarder : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
    
    private class FakeFlowControlledForwarder : IFlowControl, IAsyncDisposable
    {
        public long Requested { get; private set; }
        
        public bool Cancelled { get; private set; }
        
        public void Request(long count)
        {
            Requested += count;
        }

        public void Cancel()
        {
            Cancelled = true;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}