using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryConfigurationTests
{
    [Fact]
    public void DefaultSectionReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.DefaultSection;

        Assert.Equal("AxonIQ", result);
    }
    
    [Fact]
    public void ComponentNameReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.ComponentName;

        Assert.Equal("ComponentName", result);
    }
    
    [Fact]
    public void ClientInstanceIdReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.ClientInstanceId;

        Assert.Equal("ClientInstanceId", result);
    }
    
    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.RoutingServers;

        Assert.Equal("RoutingServers", result);
    }
}