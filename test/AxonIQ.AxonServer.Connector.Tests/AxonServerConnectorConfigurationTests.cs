using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectorConfigurationTests
{
    [Fact]
    public void DefaultSectionReturnsExpectedResult()
    {
        var result = AxonServerConnectorConfiguration.DefaultSection;

        Assert.Equal("AxonIQ", result);
    }

    [Fact]
    public void ComponentNameReturnsExpectedResult()
    {
        var result = AxonServerConnectorConfiguration.ComponentName;

        Assert.Equal("ComponentName", result);
    }

    [Fact]
    public void ClientInstanceIdReturnsExpectedResult()
    {
        var result = AxonServerConnectorConfiguration.ClientInstanceId;

        Assert.Equal("ClientInstanceId", result);
    }

    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectorConfiguration.RoutingServers;

        Assert.Equal("RoutingServers", result);
    }
}