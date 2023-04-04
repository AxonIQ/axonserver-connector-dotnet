using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Google.Protobuf;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class QueryChannelIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public QueryChannelIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
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
    public async Task RegisterQueryHandlerWhileDisconnectedHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest(builder =>
            builder.WithRoutingServers(new DnsEndPoint("127.0.0.0", AxonServerConnectionFactoryDefaults.Port)));
        var sut = connection.QueryChannel;

        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        // NOTE: We're not using `await using var ...` here because disposing would trigger an exception that
        // we're not able to reach the server.
        var registration = await sut.RegisterQueryHandler(
            new QueryHandler(),
            queries);

        await Assert.ThrowsAsync<AxonServerException>(() => registration.WaitUntilCompleted());
    }
 
    [Fact]
    public async Task RegisterQueryHandlerWhileConnectedHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        await using var registration = await sut.RegisterQueryHandler(
            new PingPongQueryHandler(responseId), 
            queries);
    
        await registration.WaitUntilCompleted();
    
        var result = sut.Query(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        var response = Assert.Single(actual);
        Assert.Equal(responseId.ToString(), response.MessageIdentifier);
        Assert.Equal(requestId.ToString(), response.RequestIdentifier);
        Assert.Equal("pong", response.Payload.Type);
        Assert.Equal("0", response.Payload.Revision);
        Assert.Equal(ByteString.CopyFromUtf8("{ \"pong\": true }").ToByteArray(), response.Payload.Data.ToByteArray());
    }
    
    [Fact]
    public async Task UnregisterQueryHandlerWhileConnectedHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        await using var registration = await sut.RegisterQueryHandler(
            new PingPongQueryHandler(responseId), 
            queries);
    
        await registration.WaitUntilCompleted();
        
        await registration.DisposeAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        var response = Assert.Single(actual);
        Assert.Equal(ErrorCategory.NoHandlerForQuery.ToString(), response.ErrorCode);
        Assert.Equal("No handler for query: Ping", response.ErrorMessage.Message);
        Assert.Equal("AxonServer", response.ErrorMessage.Location);
    }

    [Fact]
    public async Task QueryWithManyResponsesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        await using var registration = await sut.RegisterQueryHandler(
            new OnePingManyPongQueryHandler(responseIds), 
            queries);
    
        await registration.WaitUntilCompleted();
    
        var result = sut.Query(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        Assert.Equal(3, actual.Length);
        Assert.Equal(responseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(responseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(responseIds[2].ToString(), actual[2].MessageIdentifier);
    }
    
    [Fact]
    public async Task SubscriptionQueryWaitForInitialResultHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        await using var registration = await sut.RegisterQueryHandler(
            new PingPongQueryHandler(responseId), 
            queries);
    
        await registration.WaitUntilCompleted();
    
        var result = await sut.SubscriptionQuery(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject(), new PermitCount(1), new PermitCount(1), CancellationToken.None);

        var actual = await result.InitialResult;
        Assert.Equal(responseId.ToString(), actual.MessageIdentifier);
        Assert.Equal(requestId.ToString(), actual.RequestIdentifier);
        Assert.Equal("pong", actual.Payload.Type);
        Assert.Equal("0", actual.Payload.Revision);
        Assert.Equal(ByteString.CopyFromUtf8("{ \"pong\": true }").ToByteArray(), actual.Payload.Data.ToByteArray());
    }
    
    [Fact(Skip = "Needs work")]
    public async Task SubscriptionQueryWaitForUpdatesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        await using var registration = await sut.RegisterQueryHandler(
            new OnePingManyPongQueryHandler(responseIds), 
            queries);
    
        await registration.WaitUntilCompleted();
    
        var result = await sut.SubscriptionQuery(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject(), new PermitCount(1), new PermitCount(1), CancellationToken.None);

        var actual = await result.Updates.ToArrayAsync();
        Assert.Equal(3, actual.Length);
        Assert.Equal(responseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(responseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(responseIds[2].ToString(), actual[2].MessageIdentifier);
    }

    private class QueryHandler : IQueryHandler
    {
        public Task Handle(QueryRequest request, IQueryResponseChannel responseChannel)
        {
            return Task.CompletedTask;
        }

        public ISubscriptionQueryRegistration? RegisterSubscriptionQuery(SubscriptionQuery query,
            ISubscriptionQueryUpdateResponseChannel responseChannel)
        {
            return null;
        }
    }
    
    private class PingPongQueryHandler : IQueryHandler
    {
        private readonly InstructionId _responseId;

        public PingPongQueryHandler(InstructionId responseId)
        {
            _responseId = responseId;
        }
        
        public async Task Handle(QueryRequest request, IQueryResponseChannel responseChannel)
        {
            if (request.Query == "Ping")
            {
                await responseChannel.SendAsync(new QueryResponse
                {
                    MessageIdentifier = _responseId.ToString(),
                    RequestIdentifier = request.MessageIdentifier,
                    Payload = new SerializedObject
                    {
                        Type = "pong",
                        Revision = "0",
                        Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                    }
                });
                await responseChannel.CompleteAsync();
            }
        }

        public ISubscriptionQueryRegistration? RegisterSubscriptionQuery(SubscriptionQuery query,
            ISubscriptionQueryUpdateResponseChannel responseChannel)
        {
            return null;
        }
    }
    
    private class OnePingManyPongQueryHandler : IQueryHandler
    {
        private readonly InstructionId[] _responseIds;

        public OnePingManyPongQueryHandler(InstructionId[] responseIds)
        {
            _responseIds = responseIds;
        }
        
        public async Task Handle(QueryRequest request, IQueryResponseChannel responseChannel)
        {
            if (request.Query == "Ping")
            {
                foreach (var responseId in _responseIds)
                {
                    await responseChannel.SendAsync(new QueryResponse
                    {
                        MessageIdentifier = responseId.ToString(),
                        RequestIdentifier = request.MessageIdentifier,
                        Payload = new SerializedObject
                        {
                            Type = "pong",
                            Revision = "0",
                            Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                        }
                    });
                }

                await responseChannel.CompleteAsync();
            }
        }

        public ISubscriptionQueryRegistration? RegisterSubscriptionQuery(SubscriptionQuery query,
            ISubscriptionQueryUpdateResponseChannel responseChannel)
        {
            return null;
        }
    }
    
    private static class PingTypeCounter
    {
        private static int _current = -1;

        public static int Next()
        {
            return Interlocked.Increment(ref _current);
        } 
    }
}