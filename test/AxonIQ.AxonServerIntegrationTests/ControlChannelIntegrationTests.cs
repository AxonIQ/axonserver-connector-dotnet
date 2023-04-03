using AutoFixture;
using AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.IntegrationTests;

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
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }
    
    [Fact]
    public async Task SendingInstructionsWithoutIdAreCompletedImmediately()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        var result = sut.SendInstruction(new PlatformInboundInstruction());
        Assert.True(result.IsCompleted);
    }
    
    // [Fact(Skip = "This needs work")]
    // public async Task RecoveryAfterMissedHeartbeat()
    // {
    //     var interceptor = new ControlledAvailabilityInterceptor();
    //
    //     var connection = await CreateSystemUnderTest(options => options.WithInterceptors(interceptor));
    //     var sut = connection.ControlChannel;
    //     await connection.WaitUntilReadyAsync();
    //     
    //     await sut.EnableHeartbeat(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    //     // Allow the heartbeat pump to start
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //     
    //     Assert.True(connection.IsConnected);
    //     Assert.True(connection.IsReady);
    //     
    //     // Observe a disconnect
    //     var disconnected = connection.WaitUntilClosedAsync();
    //     
    //     //Simulate unavailability
    //     interceptor.Available = false;
    //
    //     await disconnected.WaitAsync(TimeSpan.FromSeconds(5));
    //     
    //     Assert.False(connection.IsConnected);
    //     Assert.False(connection.IsReady);
    //     
    //     //Observe a connect
    //     var connected = connection.WaitUntilConnectedAsync();
    //     
    //     //Simulate availability
    //     interceptor.Available = true;
    //     
    //     await connected.WaitAsync(TimeSpan.FromSeconds(5));
    //     
    //     Assert.True(connection.IsConnected);
    //     Assert.True(connection.IsReady);
    // }
}