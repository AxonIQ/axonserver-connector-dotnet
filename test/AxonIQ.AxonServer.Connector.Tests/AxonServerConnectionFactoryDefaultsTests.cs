using System.Net;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryDefaultsTests
{
    [Fact]
    public void PortReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.Port;

        Assert.Equal(8124, result);
    }
    
    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.RoutingServers;

        Assert.Equal(new List<DnsEndPoint>
        {
            new("localhost", 8124)
        }, result);
    }

    [Fact]
    public void ClientTagsReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.ClientTags;

        Assert.Empty(result);
    }

    [Fact]
    public void AuthenticationReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.Authentication;

        Assert.Same(AxonServerAuthentication.None, result);
    }
    
    [Fact]
    public void MinimumCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.MinimumCommandPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.DefaultCommandPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
    
    [Fact]
    public void MinimumQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.MinimumQueryPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionDefaults.DefaultQueryPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }

    [Fact]
    public void DefaultReconnectOptions()
    {
        var result = AxonServerConnectionDefaults.DefaultReconnectOptions;
        
        Assert.Equal(new ReconnectOptions(
            TimeSpan.FromMilliseconds(10000),
            TimeSpan.FromMilliseconds(2000),
            true), result);
    }
}