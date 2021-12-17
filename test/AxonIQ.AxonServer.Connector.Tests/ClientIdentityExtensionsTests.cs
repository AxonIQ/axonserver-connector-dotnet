using AutoFixture;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ClientIdentityExtensionsTests
{
    [Fact]
    public void ToClientIdentificationReturnsExpectedResult()
    {
        var fixture = new Fixture();
        fixture.CustomizeClientInstanceId();
        fixture.CustomizeComponentName();

        var component = fixture.Create<ComponentName>();
        var clientInstanceId = fixture.Create<ClientInstanceId>();
        var tags = fixture.Create<Dictionary<string, string>>();
        var version = fixture.Create<Version>();

        var sut = new ClientIdentity(component, clientInstanceId, tags, version);

        var result = sut.ToClientIdentification();

        Assert.Equal(component.ToString(), result.ComponentName);
        Assert.Equal(clientInstanceId.ToString(), result.ClientId);
        Assert.Equal(tags, result.Tags);
        Assert.Equal(version.ToString(), result.Version);
    }
}