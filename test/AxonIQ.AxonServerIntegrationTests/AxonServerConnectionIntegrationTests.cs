using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
[Trait("Surface", "Connection")]
public class AxonServerConnectionIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly ITestOutputHelper _output;
    private readonly Fixture _fixture;
    private readonly ILogger _logger;

    public AxonServerConnectionIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _output = output;
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _logger = new TestOutputHelperLogger(output);
    }

    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectorOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(new TestOutputHelperLoggerFactory(_output));
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }

    private async Task<IAxonServerConnection> CreateDisposedSystemUnderTest(
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var sut = await CreateSystemUnderTest(configure);
        await sut.DisposeAsync();
        return sut;
    }

    [Fact]
    public async Task WhenDisposingWaitUntilConnectedAsyncReturnsExpectedResult()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithRoutingServers(
                new DnsEndPoint("127.0.0.0", AxonServerConnectorDefaults.Port)));
        var wait = sut.WaitUntilConnectedAsync();
        await sut.DisposeAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);
    }
    
    [Fact]
    public async Task WhenDisposingWaitUntilReadyAsyncReturnsExpectedResult()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithRoutingServers(
                new DnsEndPoint("127.0.0.0", AxonServerConnectorDefaults.Port)));
        var wait = sut.WaitUntilReadyAsync();
        await sut.DisposeAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);
    }
    
    [Fact]
    public async Task WhenDisposedIsConnectedReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.False(sut.IsConnected);
    }
    
    [Fact]
    public async Task WhenDisposedIsClosedReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.True(sut.IsClosed);
    }
    
    [Fact]
    public async Task WhenDisposedIsReadyReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.False(sut.IsReady);
    }
    
    [Fact]
    public async Task WhenDisposedWaitUntilConnectedAsyncReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sut.WaitUntilConnectedAsync());
    }
    
    [Fact]
    public async Task WhenDisposedWaitUntilReadyAsyncReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await sut.WaitUntilReadyAsync());
    }
    
    [Fact]
    public async Task WhenDisposedCloseAsyncReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        await sut.CloseAsync();
    }
    
    [Fact]
    public async Task WhenDisposedAdminChannelReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.Throws<ObjectDisposedException>(() => sut.AdminChannel);
    }
    
    [Fact]
    public async Task WhenDisposedControlChannelReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.Throws<ObjectDisposedException>(() => sut.ControlChannel);
    }
    
    [Fact]
    public async Task WhenDisposedCommandChannelReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.Throws<ObjectDisposedException>(() => sut.CommandChannel);
    }
    
    [Fact]
    public async Task WhenDisposedQueryChannelReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.Throws<ObjectDisposedException>(() => sut.QueryChannel);
    }
    
    [Fact]
    public async Task WhenDisposedEventChannelReturnsExpectedResult()
    {
        await using var sut = await CreateDisposedSystemUnderTest();
        Assert.Throws<ObjectDisposedException>(() => sut.EventChannel);
    }
    
    [Fact]
    public async Task IsConnectedReturnsExpectedResult()
    {
        await using var sut = await CreateSystemUnderTest(
            configure => configure.WithGrpcChannelOptions(new GrpcChannelOptions
            {
                HttpHandler = new DelayedHandler
                {
                    ResponseDelay = TimeSpan.FromSeconds(1),
                    InnerHandler = new SocketsHttpHandler()
                },
                DisposeHttpClient = true
            }));
        
        Assert.False(sut.IsConnected);

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        Assert.True(sut.IsConnected);
    }
    
    // [Fact]
    // public async Task WaitUntilConnectedEventIsFiredOnceConnectionIsEstablished()
    // {
    //     await using var sut = await CreateSystemUnderTest();
    //
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Connected += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    // }
    
    // [Fact]
    // public async Task DisconnectedEventIsFiredOnceConnectionIsShutdown()
    // {
    //     await using var sut = await CreateSystemUnderTest();
    //
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Disconnected += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //
    //     await sut.Disconnect();
    //     
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    // }
    
    // [Fact]
    // public async Task IsDisconnectedReturnsExpectedResultOnceConnectionIsShutdown()
    // {
    //     await using var sut = await CreateSystemUnderTest();
    //
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Disconnected += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //     await sut.Disconnect();
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    //     Assert.True(sut.IsDisconnected);
    // }
    
    // [Fact]
    // public async Task IsConnectedReturnsExpectedResultOnceConnectionIsEstablished()
    // {
    //     await using var sut = await CreateSystemUnderTest();
    //
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Connected += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    //     Assert.True(sut.IsConnected);
    // }
    
    [Fact]
    public async Task IsReadyReturnsExpectedResult()
    {
        await using var sut = await CreateSystemUnderTest(
            configure => configure.WithGrpcChannelOptions(new GrpcChannelOptions
            {
                HttpHandler = new DelayedHandler
                {
                    ResponseDelay = TimeSpan.FromSeconds(1),
                    InnerHandler = new HttpClientHandler()
                },
                DisposeHttpClient = true
            }));
        
        Assert.False(sut.IsReady);

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        Assert.True(sut.IsReady);
    }
    
    // [Fact]
    // public async Task IsReadyReturnsExpectedResultOnceControlChannelIsConnected()
    // {
    //     await using var sut = await CreateSystemUnderTest();
    //
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Ready += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    //     Assert.True(sut.IsConnected, "Connection not connected");
    //     Assert.True(((ControlChannel)sut.ControlChannel).IsConnected, "ControlChannel not connected");
    //     Assert.True(sut.IsReady, "Connection not ready");
    // }

    [Fact(Skip = "Just to get a sense of the message flow - for manual testing only")]
    public async Task ShowTheFlow()
    {
        await using var sut = await CreateSystemUnderTest();

        await sut.ControlChannel.EnableHeartbeatAsync(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    // [Fact(Skip = "Needs work")]
    // public async Task IsConnectedReturnsExpectedResultWhenConnectionIsRecoveredByHeartbeat()
    // {
    //     var handler = new DelayedHandler
    //     {
    //         ResponseDelay = TimeSpan.FromSeconds(1),
    //         InnerHandler = new HttpClientHandler()
    //     };
    //     await using var sut = await CreateSystemUnderTest(
    //         configure =>
    //         {
    //             configure.WithGrpcChannelOptions(new GrpcChannelOptions
    //             {
    //                 HttpHandler = handler,
    //                 DisposeHttpClient = true
    //             });
    //         });
    //     await sut.ControlChannel.EnableHeartbeat(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    //     var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); 
    //     sut.Connected += (_, _) =>
    //     {
    //         source.TrySetResult();
    //     };
    //     source.Task.Wait(TimeSpan.FromSeconds(10));
    //     Assert.True(sut.IsConnected);
    //     
    //     _logger.LogDebug("Connection status is READY");
    //     
    //     var maximumWaitTimeForHeartbeatToStabilize = TimeSpan.FromSeconds(2);
    //     var totalWaitTimeForHeartbeatToStabilize = TimeSpan.Zero;
    //     var waitTimeBetweenPollingIsConnected = TimeSpan.FromMilliseconds(100);
    //     while (totalWaitTimeForHeartbeatToStabilize < maximumWaitTimeForHeartbeatToStabilize)
    //     {
    //         Assert.True(sut.IsConnected);
    //         await Task.Delay(waitTimeBetweenPollingIsConnected);
    //         totalWaitTimeForHeartbeatToStabilize += waitTimeBetweenPollingIsConnected;
    //     }
    //     
    //     _logger.LogDebug("Simulating bad connection");
    //     
    // }
}