using System.Net;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryDefaultsTests
{
    [Fact]
    public void PortReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.Port;

        Assert.Equal(8124, result);
    }
    
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
    
    [Fact]
    public void MinimumCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.MinimumCommandPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.DefaultCommandPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
    
    [Fact]
    public void MinimumQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.MinimumQueryPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.DefaultQueryPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
}