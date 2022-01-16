using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Grpc.Control;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class ControlChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public ControlChannelIntegrationTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _loggerFactory = new NullLoggerFactory();
    }
    
    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint());
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.Connect(Context.Default);
    }
    
    [Fact]
    public async Task SendingInstructionsWithoutIdAreCompletedImmediately()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        var result = sut.SendInstruction(new PlatformInboundInstruction());
        Assert.True(result.IsCompleted);
    }
}