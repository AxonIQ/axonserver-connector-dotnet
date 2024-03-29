using System.Net;
using AutoFixture;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryTests
{
    private readonly Fixture _fixture;

    public AxonServerConnectionFactoryTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
    }

    [Fact]
    public void OptionsCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AxonServerConnectionFactory(null!));
    }

    [Fact]
    public void ConstructionHasExpectedResult()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstanceId = _fixture.Create<ClientInstanceId>();
        var servers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();
        var tags = _fixture.CreateMany<KeyValuePair<string, string>>(Random.Shared.Next(1, 5)).ToArray();
        var token = _fixture.Create<string>();
        var builder =
            AxonServerConnectorOptions
                .For(component, clientInstanceId)
                .WithRoutingServers(servers)
                .WithClientTags(tags)
                .WithAuthenticationToken(token);
        var options = builder.Build();
        var sut = new AxonServerConnectionFactory(options);

        Assert.Equal(component, sut.ClientIdentity.ComponentName);
        Assert.Equal(clientInstanceId, sut.ClientIdentity.ClientInstanceId);
        Assert.Equal(tags, sut.ClientIdentity.ClientTags);
        Assert.Equal(new Version(1, 0), sut.ClientIdentity.Version);
        Assert.Equal(servers, sut.RoutingServers);
        var authentication = Assert.IsType<TokenBasedServerAuthentication>(sut.Authentication);
        Assert.Equal(token, authentication.Token);
        Assert.IsType<NullLoggerFactory>(sut.LoggerFactory);
    }
}