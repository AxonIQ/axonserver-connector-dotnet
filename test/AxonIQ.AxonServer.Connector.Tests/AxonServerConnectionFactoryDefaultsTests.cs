using System.Net;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryDefaultsTests
{
    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.RoutingServers;

        Assert.Equal(new List<DnsEndPoint>
        {
            new("localhost", 8124)
        }, result);
    }

    [Fact]
    public void ClientTagsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.ClientTags;

        Assert.Empty(result);
    }

    [Fact]
    public void AuthenticationReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.Authentication;

        Assert.Same(AxonServerAuthentication.None, result);
    }

    [Fact]
    public void ConnectTimeoutReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.ConnectTimeout;

        Assert.Equal(TimeSpan.FromMilliseconds(10_000), result);
    }
}