using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
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
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(new TestOutputHelperLoggerFactory(_output));
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.Connect(Context.Default);
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
                    InnerHandler = new HttpClientHandler()
                },
                DisposeHttpClient = true
            }));
        
        Assert.False(sut.IsConnected);

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        Assert.True(sut.IsConnected);
    }
    
    [Fact]
    public async Task ConnectedEventIsFiredOnceConnectionIsEstablished()
    {
        await using var sut = await CreateSystemUnderTest();

        var source = new TaskCompletionSource(); 
        sut.Connected += (_, _) =>
        {
            source.TrySetResult();
        };
        source.Task.Wait(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task IsConnectedReturnsExpectedResultOnceConnectionIsEstablished()
    {
        await using var sut = await CreateSystemUnderTest();

        var source = new TaskCompletionSource(); 
        sut.Connected += (_, _) =>
        {
            source.TrySetResult();
        };
        source.Task.Wait(TimeSpan.FromSeconds(10));
        Assert.True(sut.IsConnected);
    }
    
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
    
    [Fact]
    public async Task IsReadyReturnsExpectedResultOnceControlChannelIsConnected()
    {
        await using var sut = await CreateSystemUnderTest();

        var source = new TaskCompletionSource(); 
        sut.Ready += (_, _) =>
        {
            source.TrySetResult();
        };
        source.Task.Wait(TimeSpan.FromSeconds(10));
        Assert.True(sut.IsConnected, "Connection not connected");
        Assert.True(((ControlChannel)sut.ControlChannel).IsConnected, "ControlChannel not connected");
        Assert.True(sut.IsReady, "Connection not ready");
    }

    [Fact(Skip = "Just to get a sense of the message flow - for manual testing only")]
    public async Task ShowTheFlow()
    {
        await using var sut = await CreateSystemUnderTest();

        await sut.ControlChannel.EnableHeartbeat(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    [Fact(Skip = "Needs work")]
    public async Task IsConnectedReturnsExpectedResultWhenConnectionIsRecoveredByHeartbeat()
    {
        var handler = new DelayedHandler
        {
            ResponseDelay = TimeSpan.FromSeconds(1),
            InnerHandler = new HttpClientHandler()
        };
        await using var sut = await CreateSystemUnderTest(
            configure =>
            {
                configure.WithGrpcChannelOptions(new GrpcChannelOptions
                {
                    HttpHandler = handler,
                    DisposeHttpClient = true
                });
            });
        await sut.ControlChannel.EnableHeartbeat(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        var source = new TaskCompletionSource(); 
        sut.Connected += (_, _) =>
        {
            source.TrySetResult();
        };
        source.Task.Wait(TimeSpan.FromSeconds(10));
        Assert.True(sut.IsConnected);
        
        _logger.LogDebug("Connection status is READY");
        
        var maximumWaitTimeForHeartbeatToStabilize = TimeSpan.FromSeconds(2);
        var totalWaitTimeForHeartbeatToStabilize = TimeSpan.Zero;
        var waitTimeBetweenPollingIsConnected = TimeSpan.FromMilliseconds(100);
        while (totalWaitTimeForHeartbeatToStabilize < maximumWaitTimeForHeartbeatToStabilize)
        {
            Assert.True(sut.IsConnected);
            await Task.Delay(waitTimeBetweenPollingIsConnected);
            totalWaitTimeForHeartbeatToStabilize += waitTimeBetweenPollingIsConnected;
        }
        
        _logger.LogDebug("Simulating bad connection");
        
    }
}