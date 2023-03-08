using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(ToxicAxonServerWithAccessControlDisabledCollection))]
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
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcProxyEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.Connect(Context.Default);
    }
    
    [Fact(Skip = "This needs work")]
    public async Task RecoveryAfterConnectionReset()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        await connection.WaitUntilReady();
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);

        await sut.EnableHeartbeat(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        
        // Observe connection loss
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Disconnected += (_, _) =>
        {
            disconnected.TrySetResult();
        };
        
        //await _container.DisableGrpcProxyEndpointAsync();
        var reset = await _container.ResetPeerOnGrpcProxyEndpointAsync();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(connection.IsConnected);
        Assert.False(connection.IsReady);
        
        // Observe recovery
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Connected += (_, _) =>
        {
            connected.TrySetResult();
        };
        
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Ready += (_, _) =>
        {
            ready.TrySetResult();
        };

        await reset.DisposeAsync();
        //await _container.EnableGrpcProxyEndpointAsync();
        
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(connection.IsConnected);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(connection.IsReady);
    }
    
    [Fact]
    public async Task RecoveryAfterConnectionLoss()
    {
        var connection = await CreateSystemUnderTest();
        var sut = connection.ControlChannel;
        await connection.WaitUntilReady();
        Assert.True(connection.IsConnected);
        Assert.True(connection.IsReady);

        //await sut.EnableHeartbeat(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        
        // Observe connection loss
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Disconnected += (_, _) =>
        {
            disconnected.TrySetResult();
        };
        
        await _container.DisableGrpcProxyEndpointAsync();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(connection.IsConnected);
        Assert.False(connection.IsReady);
        
        // Observe recovery
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Connected += (_, _) =>
        {
            connected.TrySetResult();
        };
        
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Ready += (_, _) =>
        {
            ready.TrySetResult();
        };

        await _container.EnableGrpcProxyEndpointAsync();
        
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(connection.IsConnected);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(connection.IsReady);
    }
}