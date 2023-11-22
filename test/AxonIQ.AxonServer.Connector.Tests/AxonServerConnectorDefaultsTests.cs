using System.Net;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectorDefaultsTests
{
    [Fact]
    public void PortReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.Port;

        Assert.Equal(8124, result);
    }
    
    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.RoutingServers;

        Assert.Equal(new List<DnsEndPoint>
        {
            new("localhost", 8124)
        }, result);
    }

    [Fact]
    public void ClientTagsReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.ClientTags;

        Assert.Empty(result);
    }

    [Fact]
    public void AuthenticationReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.Authentication;

        Assert.Same(AxonServerAuthentication.None, result);
    }
    
    [Fact]
    public void MinimumCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.MinimumCommandPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.DefaultCommandPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
    
    [Fact]
    public void MinimumQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.MinimumQueryPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectorDefaults.DefaultQueryPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }

    [Fact]
    public void DefaultReconnectOptions()
    {
        var result = AxonServerConnectorDefaults.DefaultReconnectOptions;
        
        Assert.Equal(new ReconnectOptions(
            TimeSpan.FromMilliseconds(10000),
            TimeSpan.FromMilliseconds(2000),
            true), result);
    }
    
    [Fact]
    public void DefaultChannelInstructionTimeout()
    {
        var result = AxonServerConnectorDefaults.DefaultChannelInstructionTimeout;
        
        Assert.Equal(TimeSpan.FromSeconds(15), result);
    }
    
    [Fact]
    public void DefaultChannelInstructionPurgeFrequency()
    {
        var result = AxonServerConnectorDefaults.DefaultChannelInstructionPurgeFrequency;
        
        Assert.Equal(TimeSpan.FromSeconds(5), result);
    }
}