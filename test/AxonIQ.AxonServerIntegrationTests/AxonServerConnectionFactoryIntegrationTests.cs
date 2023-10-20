using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Xunit;

namespace AxonIQ.AxonServerIntegrationTests;

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
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectorOptions.For(component, clientInstance)
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

        var result = await sut.ConnectAsync(context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SuccessiveConnectToSameContextReturnsSameInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(context);
        var second = await sut.ConnectAsync(context);

        Assert.Same(first, second);
        
        await first.DisposeAsync();
    }
    
    [Fact]
    public async Task SuccessiveConnectToSameButDisposedContextReturnsNewInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(context);
        await first.DisposeAsync();
        
        var second = await sut.ConnectAsync(context);

        Assert.NotSame(first, second);

        await second.DisposeAsync();
    }
    
    [Fact]
    public async Task DisposingHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(Context.Default);
        var second = await sut.ConnectAsync(Context.Admin);

        await sut.DisposeAsync();
        
        Assert.Throws<ObjectDisposedException>(() => first.ControlChannel);
        Assert.Throws<ObjectDisposedException>(() => second.ControlChannel);
    }
}