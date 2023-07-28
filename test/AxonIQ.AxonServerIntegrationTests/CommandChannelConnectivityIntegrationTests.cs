using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(ToxicAxonServerWithAccessControlDisabledCollection))]
public class CommandChannelConnectivityIntegrationTests
{
    private readonly IToxicAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public CommandChannelConnectivityIntegrationTests(ToxicAxonServerWithAccessControlDisabled container, ITestOutputHelper output)
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
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)))
            .ConfigureAwait(false);
        await connection.WaitUntilReadyAsync();
        
        var responseId = InstructionId.New().ToString();
        
        await using var registration = await connection.CommandChannel.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId,
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
            }
        }), new LoadFactor(1), new CommandName("ping"));
        
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);

        var requestId1 = InstructionId.New().ToString();
        var response1 = await connection
            .CommandChannel
            .SendCommandAsync(new Command
            {
                MessageIdentifier = requestId1,
                Name = "ping",
                Payload = new SerializedObject { Data = ByteString.CopyFromUtf8("{}"), Type = "ping", Revision = "0"}
            }, CancellationToken.None);

        Assert.Equal(requestId1, response1.RequestIdentifier);
        
        await _container.DisableGrpcProxyEndpointAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(connection.IsConnected);
        Assert.False(connection.IsReady);

        await Assert.ThrowsAsync<AxonServerException>(async () => await connection
            .CommandChannel
            .SendCommandAsync(new Command
            {
                Name = "ping",
                Payload = new SerializedObject { Data = ByteString.CopyFromUtf8("{}"), Type = "ping", Revision = "0"}
            }, CancellationToken.None));

        await _container.EnableGrpcProxyEndpointAsync();
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        
        var requestId2 = InstructionId.New().ToString();
        var response2 = await connection
            .CommandChannel
            .SendCommandAsync(new Command
            {
                MessageIdentifier = requestId2,
                Name = "ping",
                Payload = new SerializedObject { Data = ByteString.CopyFromUtf8("{}"), Type = "ping", Revision = "0"}
            }, CancellationToken.None);

        Assert.Equal(requestId2, response2.RequestIdentifier);
    }

    [Fact]
    public async Task RecoverFromHeartbeatMissed()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        await connection.ControlChannel.EnableHeartbeatAsync(
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(500));
        
        var responseId = InstructionId.New().ToString();
        
        await using var registration = await connection.CommandChannel.RegisterCommandHandlerAsync((command, ct) => Task.FromResult(new CommandResponse
        {
            MessageIdentifier = responseId,
            Payload = new SerializedObject
            {
                Type = "pong",
                Revision = "0",
                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
            }
        }), new LoadFactor(1), new CommandName("ping"));

        await using (await _container.TimeoutEndpointAsync())
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(connection.IsConnected);
            Assert.False(connection.IsReady);
            
            await Assert.ThrowsAsync<AxonServerException>(async () => await connection
                .CommandChannel
                .SendCommandAsync(new Command
                {
                    Name = "ping",
                    Payload = new SerializedObject { Data = ByteString.CopyFromUtf8("{}"), Type = "ping", Revision = "0"}
                }, CancellationToken.None));
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);
        
        var requestId1 = InstructionId.New().ToString();
        var response1 = await connection
            .CommandChannel
            .SendCommandAsync(new Command
            {
                MessageIdentifier = requestId1,
                Name = "ping",
                Payload = new SerializedObject { Data = ByteString.CopyFromUtf8("{}"), Type = "ping", Revision = "0"}
            }, CancellationToken.None);

        Assert.Equal(requestId1, response1.RequestIdentifier);
    }
}