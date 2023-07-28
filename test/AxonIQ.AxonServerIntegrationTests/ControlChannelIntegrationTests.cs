using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class ControlChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public ControlChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }
    
    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectorOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }
    
    [Fact(Skip = "Need to figure out how to test this without reaching for the internals")]
    public async Task SendingInstructionWithoutIdHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        var result = sut.SendInstructionAsync(new PlatformInboundInstruction());
        Assert.True(result.IsCompleted);
    }
    
    [Fact(Skip = "Need to figure out how to test this without reaching for the internals")]
    public async Task SendingInstructionWithIdHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        var result = sut.SendInstructionAsync(new PlatformInboundInstruction());
        Assert.True(result.IsCompleted);
    }
}