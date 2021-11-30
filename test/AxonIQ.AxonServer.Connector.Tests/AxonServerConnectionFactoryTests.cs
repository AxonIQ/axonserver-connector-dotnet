using System.Net;
using AutoFixture;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryTests
{
    private readonly Fixture _fixture;

    public AxonServerConnectionFactoryTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeClientId();
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
        var clientInstanceId = _fixture.Create<ClientId>();
        var servers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();
        var tags = _fixture.CreateMany<KeyValuePair<string, string>>(Random.Shared.Next(1, 5)).ToArray();
        var token = _fixture.Create<string>();
        var builder =
            AxonServerConnectionFactoryOptions
                .For(component, clientInstanceId)
                .WithRoutingServers(servers)
                .WithClientTags(tags)
                .WithAuthenticationToken(token);
        var options = builder.Build();
        var sut = new AxonServerConnectionFactory(options);

        Assert.Equal(component, sut.ComponentName);
        Assert.Equal(clientInstanceId, sut.ClientInstanceId);
        Assert.Equal(servers, sut.RoutingServers);
        Assert.Equal(tags, sut.ClientTags);
        var authentication = Assert.IsType<TokenBasedServerAuthentication>(sut.Authentication);
        Assert.Equal(token, authentication.Token);
    }

    private AxonServerConnectionFactory CreateSystemUnderTest()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientId>();

        return new AxonServerConnectionFactory(
            AxonServerConnectionFactoryOptions.For(component, clientInstance).Build());
    }

    [Fact]
    public Task ConnectContextCanNotBeNull()
    {
        return Assert.ThrowsAsync<ArgumentNullException>(async () => await CreateSystemUnderTest().Connect(null!));
    }

    [Fact]
    public async Task ConnectContextReturnsExpectedResult()
    {
        var context = _fixture.Create<string>();
        var sut = CreateSystemUnderTest();

        var result = await sut.Connect(context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SuccessiveConnectToSameContextReturnsSameInstance()
    {
        var context = _fixture.Create<string>();
        var sut = CreateSystemUnderTest();

        var first = await sut.Connect(context);
        var second = await sut.Connect(context);

        Assert.Same(first, second);
    }
}