using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class BackoffPolicyOptionsTests
{
    [Fact]
    public void InitialBackoffCanNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicyOptions(
            TimeSpan.FromMilliseconds(-1),
            TimeSpan.Zero,
            1.0));
    }

    [Fact]
    public void MaximumBackoffCanNotBeLessThanInitialBackoff()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicyOptions(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(-1),
            1.0));
    }

    [Fact]
    public void BackoffMultiplierCanNotBeLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicyOptions(
            TimeSpan.Zero,
            TimeSpan.Zero,
            0.9));
    }

    [Fact]
    public void NextReturnsExpectedResultWhenCurrentIsZero()
    {
        var policy = new BackoffPolicyOptions(
            TimeSpan.Zero,
            TimeSpan.Zero,
            1.0);
        
    }
    
    [Fact]
    public void NextReturnsExpectedResultWhenCurrentIsNegative()
    {
        var policy = new BackoffPolicyOptions(
            TimeSpan.Zero,
            TimeSpan.Zero,
            1.0);
        
    }
}