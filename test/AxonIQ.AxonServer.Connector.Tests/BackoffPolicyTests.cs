using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class BackoffPolicyTests
{
    private readonly BackoffPolicy _sut;

    public BackoffPolicyTests()
    {
        var options = new BackoffPolicyOptions(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(40),
            2);
        _sut = new BackoffPolicy(options);
    }
    
    [Fact]
    public void NextReturnsInitialBackOffOnFirstCall()
    {
        var result = _sut.Next();

        Assert.Equal(_sut.Options.InitialBackoff, result);
    }

    [Fact]
    public void NextReturnsNextBackOffOnSecondCall()
    {
        _sut.Next();
        
        var result = _sut.Next();

        Assert.Equal(TimeSpan.FromMilliseconds(20), result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NextReturnsMaximumBackOffAfterSecondCall(int initialCallCount)
    {
        var options = new BackoffPolicyOptions(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromSeconds(10),
            2);
        for (var call = 0; call < initialCallCount; call++)
        {
            _sut.Next();    
        }
        
        var result = _sut.Next();

        Assert.Equal(_sut.Options.MaximumBackoff, result);
    }
}