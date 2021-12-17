using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionHeadersTests
{
    [Fact]
    public void AccessTokenReturnsExpectedResult()
    {
        var result = AxonServerConnectionHeaders.AccessToken;

        Assert.Equal("AxonIQ-Access-Token", result);
    }

    [Fact]
    public void ContextReturnsExpectedResult()
    {
        var result = AxonServerConnectionHeaders.Context;

        Assert.Equal("AxonIQ-Context", result);
    }
}