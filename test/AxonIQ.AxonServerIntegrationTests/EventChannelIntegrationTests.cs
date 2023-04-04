using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class EventChannelIntegrationTests : IAsyncLifetime
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public EventChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
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
    
    private Event CreateAggregateSnapshot(AggregateId id, long sequence, string type, string payload)
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
            Snapshot = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    [Fact]
    public async Task StartAppendEventsTransactionHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        
        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        using var transaction = sut.StartAppendEventsTransaction();
        
        Assert.NotNull(transaction);
    }
    
    [Fact]
    public async Task CommittingAppendEventsTransactionHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();
        
        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
    
        using var transaction = sut.StartAppendEventsTransaction();

        var expected = CreateEvent("event1");
        await transaction.AppendEventAsync(expected);
        var confirmation = await transaction.CommitAsync();
        
        Assert.True(confirmation.Success);
    }
    
    [Fact]
    public async Task OpenStreamReturnsCommittedEvents()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var count = Random.Shared.Next(1, 5);
        var expected = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateEvent("event" + index))
                .ToArray();
        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        using var stream = await sut.OpenStreamAsync(EventStreamToken.None, new PermitCount(18));
        var actual = await stream.Take(count).Select(@event => @event.Event).ToArrayAsync();
        
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task OpenAggregateStreamReturnsCommittedEvents()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();

        var id = new AggregateId("aggregate1");
        var expected = CreateAggregateEvent(id, 0L, "Aggregate", "Event1");
        var events =
            new []
            {
                CreateEvent("Event0"),
                expected,
                CreateEvent("Event2")
            };
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction.CommitAsync()).Success);

        var stream = sut.OpenStream(id);
        var actual = await stream.Take(1).ToArrayAsync();
        
        Assert.Equal(new [] { expected }, actual);
    }

    [Fact]
    public async Task DisposeAggregateStreamHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId("aggregate1");
        for (var sequence = 0L; sequence < 200L; sequence++)
        {
            using var transaction = sut.StartAppendEventsTransaction();
            
            var events =
                new[]
                {
                    CreateEvent("Event0"),
                    CreateAggregateEvent(id, sequence, "Aggregate", "Event1"),
                    CreateEvent("Event2")
                };
            foreach (var @event in events)
            {
                await transaction.AppendEventAsync(@event);
            }
        
            Assert.True((await transaction.CommitAsync()).Success);
        }

        var stream = sut.OpenStream(id);
        stream.Dispose();

        try
        {
            await foreach (var @event in stream)
            {
                Assert.NotEqual(199, @event.AggregateSequenceNumber);
            }
        }
        catch (ObjectDisposedException exception) when (exception.ObjectName == "GrpcCall")
        {
            // Expected
        }
    }

    [Fact]
    public async Task ScheduleAndCancelHaveExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var token = await sut.ScheduleEvent(Duration.FromTimeSpan(TimeSpan.FromDays(1)), CreateEvent("payload1"));
        
        var ack = await sut.CancelSchedule(token);
        
        Assert.True(ack.Success);
    }

    [Fact(Skip = "Awaiting Axon Server fix")]
    public async Task ScheduleAndRescheduleImmediatelyAndCancelHaveExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var @event = CreateEvent("payload2");
        
        var scheduleToken = await sut.ScheduleEvent(Duration.FromTimeSpan(TimeSpan.FromDays(1)), @event);
        
        var rescheduleToken = await sut.Reschedule(scheduleToken, DateTimeOffset.UtcNow, @event);
        
        var ack = await sut.CancelSchedule(rescheduleToken);
        
        Assert.True(ack.Success);
    }
    
    [Fact]
    public async Task ScheduleAndRescheduleAndCancelHaveExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var @event = CreateEvent("payload2");
        
        var scheduleToken = await sut.ScheduleEvent(Duration.FromTimeSpan(TimeSpan.FromDays(1)), @event);
        
        var rescheduleToken = await sut.Reschedule(scheduleToken, Duration.FromTimeSpan(TimeSpan.FromDays(2)), @event);
        
        var ack = await sut.CancelSchedule(rescheduleToken);
        
        Assert.True(ack.Success);
    }
    
    [Fact]
    public async Task CancelUnknownTokenHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var token = await sut.ScheduleEvent(Duration.FromTimeSpan(TimeSpan.FromDays(1)), CreateEvent("payload"));
        
        var ack1 = await sut.CancelSchedule(token);
        
        Assert.True(ack1.Success);
        
        var ack2 = await sut.CancelSchedule(token);
        
        Assert.True(ack2.Success);
    }

    [Fact]
    public async Task SubscribeUsingInitialTokenHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        using var transaction = sut.StartAppendEventsTransaction();
        var expected = new[]
        {
            CreateEvent("Event1"),
            CreateEvent("Event2")
        };

        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var token = await sut.GetFirstToken();
        using var stream = await sut.OpenStreamAsync(token, new PermitCount(10));
        var actual = await stream.Take(2).Select(@event => @event.Event).ToArrayAsync();
        Assert.Equal(expected, actual);
    }
    
    [Fact]
    public async Task ResubscribeUsingReceivedTokenHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        using var transaction = sut.StartAppendEventsTransaction();
        var event1 = CreateEvent("Event1");
        var event2 = CreateEvent("Event2");
        var expected = new[]
        {
            event1,
            event2
        };

        foreach (var @event in expected)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var token = await sut.GetFirstToken();
        using var stream1 = await sut.OpenStreamAsync(token, new PermitCount(10));
        var actual1 = await stream1.Take(1).SingleAsync();
        Assert.Equal(event1, actual1.Event);
        
        using var stream2 = await sut.OpenStreamAsync(new EventStreamToken(actual1.Token), new PermitCount(10));
        var actual2 = await stream2.Take(1).Select(@event => @event.Event).SingleAsync();
        Assert.Equal(event2, actual2);
    }
    
    [Fact]
    public async Task FindHighestSequenceHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));

        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "Event"+index, "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var actual = await sut.FindHighestSequenceAsync(id);
        Assert.Equal(new EventSequenceNumber(count - 1), actual);
    }
    
    [Fact]
    public async Task FindHighestSequenceOfNonExistingAggregateHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));

        var actual = await sut.FindHighestSequenceAsync(id);
        Assert.Equal(new EventSequenceNumber(-1L), actual);
    }

    [Fact]
    public async Task AppendSnapshotToNonExistingAggregateHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));

        var snapshot = CreateAggregateSnapshot(id, -1L, "Event1", "Event1");

        var result = await sut.AppendSnapshotAsync(snapshot);
        
        Assert.True(result.Success);
    }
    
    [Fact]
    public async Task AppendSnapshotToExistingAggregateHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));
        
        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "Event"+index, "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);
        
        var snapshot = CreateAggregateSnapshot(id, Random.Shared.Next(0, count), "Event1", "Event1");

        var result = await sut.AppendSnapshotAsync(snapshot);
        
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetFirstTokenWhenEventStoreIsEmptyHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var result = await sut.GetFirstToken();
        
        Assert.Equal(EventStreamToken.None, result);
    }
    
    [Fact]
    public async Task GetFirstTokenWhenEventStoreIsFilledHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateEvent("Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var result = await sut.GetFirstToken();
        
        Assert.Equal(EventStreamToken.None, result);
    }

    [Fact]
    public async Task GetLastTokenWhenEventStoreIsEmptyHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var result = await sut.GetLastToken();
        
        Assert.Equal(EventStreamToken.None, result);
    }
    
    [Fact]
    public async Task GetLastTokenWhenEventStoreIsFilledHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateEvent("Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var result = await sut.GetLastToken();
        
        Assert.Equal(new EventStreamToken(count - 2), result);
    }
    
    [Fact]
    public async Task GetTokenAtWhenEventStoreIsEmptyHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;

        var result = await sut.GetTokenAt(Random.Shared.NextInt64(0, long.MaxValue));
        
        Assert.Equal(EventStreamToken.None, result);
    }
    
    [Fact]
    public async Task GetTokenAtInThePastWhenEventStoreIsFilledHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateEvent("Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var result = await sut.GetTokenAt(DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds());
        
        Assert.Equal(EventStreamToken.None, result);
    }
    
    [Fact]
    public async Task GetTokenAtInTheFutureWhenEventStoreIsFilledHasExpectedResult()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        using var transaction = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateEvent("Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction.AppendEventAsync(@event);
        }

        Assert.True((await transaction.CommitAsync()).Success);

        var result = await sut.GetTokenAt(DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds());
        
        Assert.Equal(new EventStreamToken(count - 1), result);
    }

    [Fact]
    public async Task QueryEventsLive()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));
        
        using var transaction1 = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "event", "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction1.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction1.CommitAsync()).Success);
        
        await sut.AppendSnapshotAsync(
            CreateAggregateSnapshot(
                id, 
                0,
                "snapshot",
                "snapshot"));

        var stream = sut.QueryEvents("", true);

        var expected1 = Enumerable
            .Range(0, count)
            .Select(index => "Event" + index)
            .ToHashSet();
        var actual1 = await stream.Take(count).Select(entry => entry.GetValueAsString("payloadData")).ToHashSetAsync();
        
        Assert.Equal(expected1, actual1);
        
        await sut.AppendEvents(CreateEvent("LastEvent"));

        var expected2 = new HashSet<string>(new[] {"LastEvent"});
        var actual2 = await stream.Take(1).Select(entry => entry.GetValueAsString("payloadData")).ToHashSetAsync();
        
        Assert.Equal(expected2, actual2);
    }
    
    [Fact]
    public async Task QueryEventsStale()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));
        
        using var transaction1 = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "event", "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction1.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction1.CommitAsync()).Success);
        
        await sut.AppendSnapshotAsync(
            CreateAggregateSnapshot(
                id, 
                0,
                "snapshot",
                "snapshot"));

        var stream = sut.QueryEvents("", false);

        var expected1 = Enumerable
            .Range(0, count)
            .Select(index => "Event" + index)
            .ToHashSet();
        var actual1 = await stream.Select(entry => entry.GetValueAsString("payloadData")).ToHashSetAsync();
        
        Assert.Equal(expected1, actual1);
    }

    [Fact]
    public async Task QuerySnapshotEventsLive()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));
        
        using var transaction1 = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(2, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "event", "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction1.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction1.CommitAsync()).Success);
        
        await sut.AppendSnapshotAsync(
            CreateAggregateSnapshot(
                id, 
                0,
                "snapshot",
                "Snapshot"));

        var stream = sut.QuerySnapshotEvents("", true);

        var expected1 = "Snapshot";
        var actual1 = await stream.Take(1).Select(entry => entry.GetValueAsString("payloadData")).SingleAsync();
        
        Assert.Equal(expected1, actual1);
        
        await sut.AppendSnapshotAsync(
            CreateAggregateSnapshot(
                id, 
                1,
                "snapshot",
                "LatestSnapshot"));

        var expected2 = "LatestSnapshot";
        var actual2 = await stream.Take(1).Select(entry => entry.GetValueAsString("payloadData")).SingleAsync();
        
        Assert.Equal(expected2, actual2);
    }
    
    [Fact]
    public async Task QuerySnapshotEventsStale()
    {
        var connection = await CreateSystemUnderTest();

        await connection.WaitUntilReadyAsync();
        
        var sut = connection.EventChannel;
        
        var id = new AggregateId(Guid.NewGuid().ToString("D"));
        
        using var transaction1 = sut.StartAppendEventsTransaction();
        var count = Random.Shared.Next(1, 5);
        var events = 
            Enumerable
                .Range(0, count)
                .Select(index => CreateAggregateEvent(id, index, "event", "Event"+index))
                .ToArray();
        foreach (var @event in events)
        {
            await transaction1.AppendEventAsync(@event);
        }
        
        Assert.True((await transaction1.CommitAsync()).Success);
        
        await sut.AppendSnapshotAsync(
            CreateAggregateSnapshot(
                id, 
                0,
                "snapshot",
                "Snapshot"));

        var stream = sut.QuerySnapshotEvents("", false);

        var expected1 = "Snapshot";
        var actual1 = await stream.Select(entry => entry.GetValueAsString("payloadData")).SingleAsync();
        
        Assert.Equal(expected1, actual1);
    }
    
    public Task InitializeAsync()
    {
        return _container.PurgeEvents();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}