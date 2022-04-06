using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class FlowControllerTests
{
    [Fact]
    public void IncrementReturnsExpectedResult()
    {
        var sut = new FlowController(new PermitCount(2));

        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
        
        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
    }
    
    [Fact]
    public void ResetHasExpectedResult()
    {
        var sut = new FlowController(new PermitCount(2));

        Assert.False(sut.Increment());
        sut.Reset();
        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
    }
}