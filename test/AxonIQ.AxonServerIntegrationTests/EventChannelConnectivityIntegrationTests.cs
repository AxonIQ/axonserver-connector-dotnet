using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Google.Protobuf;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(ToxicAxonServerWithAccessControlDisabledCollection))]
public class EventChannelConnectivityIntegrationTests : IAsyncLifetime
{
    private readonly IToxicAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public EventChannelConnectivityIntegrationTests(ToxicAxonServerWithAccessControlDisabled container,
        ITestOutputHelper output)
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
    
    private Event CreateEvent(string payload)
    {
        return new Event
        {
            Payload = new SerializedObject
            {
                Data = ByteString.CopyFromUtf8(payload),
                Type = "string"
            },
            MessageIdentifier = InstructionId.New().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    private Event CreateAggregateEvent(AggregateId id, long sequence, string type, string payload)
    {
        return new Event
        {
            Payload = new SerializedObject
            {
                Data = ByteString.CopyFromUtf8(payload),
                Type = "string"
            },
            MessageIdentifier = InstructionId.New().ToString(),
            AggregateIdentifier = id.ToString(),
            AggregateSequenceNumber = sequence,
            AggregateType = type,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    [Fact]
    public async Task OpenStreamEnumerationThrowsOnConnectionLoss()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 50)
                .Select(index => CreateEvent("event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = sut.OpenStream(EventStreamToken.None, new PermitCount(18));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream)
                    {
                    }
                }
                catch (IOException)
                {
                    canceled.TrySetResult();
                }
                catch (RpcException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 3)));
        
        await _container.DisableGrpcProxyEndpointAsync();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task OpenStreamCooperativeCancellationOnReconnect()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 50)
                .Select(index => CreateEvent("event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = sut.OpenStream(EventStreamToken.None, new PermitCount(18));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream)
                    {
                    }
                }
                catch (RpcException exception) when (exception.InnerException is OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 3)));
        
        ((EventChannel)sut).Reconnect();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task OpenStreamCooperativeCancellationOnCancellationByCaller()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 50)
                .Select(index => CreateEvent("event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationByCaller = new CancellationTokenSource();
        var stream = sut.OpenStream(EventStreamToken.None, new PermitCount(18));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream.WithCancellation(cancellationByCaller.Token))
                    {
                    }
                }
                catch (RpcException exception) when (exception.InnerException is OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });

        await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 3)));
        
        cancellationByCaller.Cancel();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task OpenAggregateStreamEnumerationThrowsOnConnectionLoss()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var id = new AggregateId(Guid.NewGuid().ToString("N"));
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 5000)
                .Select(index => CreateAggregateEvent(id, index, "type","event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = Random.Shared.Next() % 2 == 0
            ? sut.OpenStream(id, false)
            : sut.OpenStream(id, new EventSequenceNumber(0));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream)
                    {
                    }
                }
                catch (IOException)
                {
                    canceled.TrySetResult();
                }
                catch (RpcException)
                {
                    canceled.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });

        await _container.DisableGrpcProxyEndpointAsync();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task OpenAggregateStreamCooperativeCancellationOnReconnect()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var id = new AggregateId(Guid.NewGuid().ToString("N"));
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 5000)
                .Select(index => CreateAggregateEvent(id, index, "type","event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = Random.Shared.Next() % 2 == 0
            ? sut.OpenStream(id, false)
            : sut.OpenStream(id, new EventSequenceNumber(0));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream)
                    {
                    }
                }
                catch (RpcException exception) when (exception.InnerException is OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });
        
        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(10, 25)));

        ((EventChannel)sut).Reconnect();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task OpenAggregateStreamCooperativeCancellationOnCancellationByCaller()
    {
        await using var connection = await CreateSystemUnderTest(options => 
            options
                .WithReconnectOptions(
                    new ReconnectOptions(
                        AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout, 
                        TimeSpan.FromMilliseconds(100),
                        false)));
        await connection.WaitUntilReadyAsync();

        var id = new AggregateId(Guid.NewGuid().ToString("N"));
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = 
            Enumerable
                .Range(0, 5000)
                .Select(index => CreateAggregateEvent(id, index, "type","event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationByCaller = new CancellationTokenSource();
        var stream = Random.Shared.Next() % 2 == 0
            ? sut.OpenStream(id, false)
            : sut.OpenStream(id, new EventSequenceNumber(0));
        var enumeration = Task.Run(async () =>
        {
            using (stream)
            {
                try
                {
                    await foreach (var _ in stream.WithCancellation(cancellationByCaller.Token))
                    {
                    }
                }
                catch (RpcException exception) when (exception.InnerException is OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    canceled.TrySetResult();
                }
                catch(Exception exception)
                {
                    canceled.TrySetException(exception);
                }
            }
        });

        await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(100, 125)));
        
        cancellationByCaller.Cancel();
        
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await enumeration.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    public async Task InitializeAsync()
    {
        await _container.ResetAsync();
        await  _container.PurgeEventsAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}