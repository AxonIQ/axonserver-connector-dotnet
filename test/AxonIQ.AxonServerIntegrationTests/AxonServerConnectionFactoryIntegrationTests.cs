using AutoFixture;
using AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Embedded;
using Xunit;

namespace AxonIQ.AxonServer.Connector.IntegrationTests;

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

        var result = await sut.ConnectAsync(context).ConfigureAwait(false);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SuccessiveConnectToSameContextReturnsSameInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(context).ConfigureAwait(false);
        var second = await sut.ConnectAsync(context).ConfigureAwait(false);

        Assert.Same(first, second);
        
        await first.DisposeAsync().ConfigureAwait(false);
    }
    
    [Fact]
    public async Task SuccessiveConnectToSameButDisposedContextReturnsNewInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(context).ConfigureAwait(false);
        await first.DisposeAsync().ConfigureAwait(false);
        
        var second = await sut.ConnectAsync(context).ConfigureAwait(false);

        Assert.NotSame(first, second);

        await second.DisposeAsync().ConfigureAwait(false);
    }
    
    [Fact]
    public async Task DisposingHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var first = await sut.ConnectAsync(Context.Default).ConfigureAwait(false);
        var second = await sut.ConnectAsync(Context.Admin).ConfigureAwait(false);

        await sut.DisposeAsync().ConfigureAwait(false);
        
        Assert.Throws<ObjectDisposedException>(() => first.ControlChannel);
        Assert.Throws<ObjectDisposedException>(() => second.ControlChannel);
    }
}