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

[Collection(nameof(ToxicAxonServerWithAccessControlDisabledCollection))]
[Trait("Surface", "ControlChannel")]
public class ControlChannelConnectivityIntegrationTests
{
    private readonly IToxicAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public ControlChannelConnectivityIntegrationTests(ToxicAxonServerWithAccessControlDisabled container, ITestOutputHelper output)
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
            .WithRoutingServers(_container.GetGrpcProxyEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }
    
    [Fact]
    public async Task RecoverFromConnectionLoss()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectorDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);

        await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(), 
            Heartbeat = new Heartbeat()
        });

        await _container.DisableGrpcProxyEndpointAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(connection.IsConnected);
        Assert.False(connection.IsReady);

        await Assert.ThrowsAsync<AxonServerException>(async () => await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(), 
            Heartbeat = new Heartbeat()
        }));

        await _container.EnableGrpcProxyEndpointAsync();
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        
        await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(), 
            Heartbeat = new Heartbeat()
        });
    }

    [Fact]
    public async Task RecoverFromHeartbeatMissed()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectorDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        await connection.ControlChannel.EnableHeartbeatAsync(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500));

        await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(), 
            Heartbeat = new Heartbeat()
        });

        await using (await _container.TimeoutEndpointAsync())
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(connection.IsConnected);
            Assert.False(connection.IsReady);
            
            await Assert.ThrowsAsync<AxonServerException>(async () => await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
            {
                InstructionId = InstructionId.New().ToString(), 
                Heartbeat = new Heartbeat()
            }));
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        
        await connection.ControlChannel.SendInstructionAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(), 
            Heartbeat = new Heartbeat()
        });
    }
}