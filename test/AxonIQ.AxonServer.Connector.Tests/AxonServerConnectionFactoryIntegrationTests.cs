using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Embedded;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerConnectionFactoryIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;

    public AxonServerConnectionFactoryIntegrationTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeContext();
    }

    private AxonServerConnectionFactory CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint());
        configure?.Invoke(builder);
        var options = builder.Build();
        return new AxonServerConnectionFactory(options);
    }

    [Fact]
    public async Task ConnectContextReturnsExpectedResult()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var result = await sut.Connect(context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SuccessiveConnectToSameContextReturnsSameInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.Connect(context);
        var second = await sut.Connect(context);

        Assert.Same(first, second);
    }
}