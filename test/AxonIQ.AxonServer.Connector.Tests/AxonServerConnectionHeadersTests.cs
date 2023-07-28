using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionHeadersTests
{
    [Fact]
    public void AccessTokenReturnsExpectedResult()
    {
        var result = AxonServerConnectorHeaders.AccessToken;

        Assert.Equal("AxonIQ-Access-Token", result);
    }

    [Fact]
    public void ContextReturnsExpectedResult()
    {
        var result = AxonServerConnectorHeaders.Context;

        Assert.Equal("AxonIQ-Context", result);
    }
}