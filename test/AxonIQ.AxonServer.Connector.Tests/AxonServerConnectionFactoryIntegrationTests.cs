using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerCollection))]
public class AxonServerConnectionFactoryIntegrationTests
{
    private readonly AxonServerContainer _container;
    private readonly Fixture _fixture;

    public AxonServerConnectionFactoryIntegrationTests(AxonServerContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientId();
        _fixture.CustomizeComponentName();
    }

    private AxonServerConnectionFactory CreateSystemUnderTest()
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientId>();

        var options = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .Build();
        return new AxonServerConnectionFactory(options);
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