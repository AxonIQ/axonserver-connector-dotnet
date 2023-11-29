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
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
[Trait("Surface", "QueryChannel")]
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

    [Fact(Skip = "Not sure what the behavior should be.")]
    public async Task RegisterQueryHandlerWhileDisconnectedHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest(builder =>
            builder.WithRoutingServers(new DnsEndPoint("127.0.0.0", AxonServerConnectorDefaults.Port)));
        var sut = connection.QueryChannel;

        var queries = new[]
        {
            new QueryDefinition(new QueryName("Ping"), "Pong")
        };
        // NOTE: We're not using `await using var ...` here because disposing would trigger an exception that
        // we're not able to reach the server.
        var registration = await sut.RegisterQueryHandlerAsync(
            new QueryHandler(),
            queries);

        await Assert.ThrowsAsync<AxonServerException>(() => registration.WaitUntilCompletedAsync());
    }
 
    [Fact]
    public async Task RegisterQueryHandlerWhileConnectedHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
        
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var handler = new PingPongQueryHandler(responseId);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        var response = Assert.Single(actual);
        Assert.Equal(responseId.ToString(), response.MessageIdentifier);
        Assert.Equal(requestId.ToString(), response.RequestIdentifier);
        Assert.Equal(handler.ResponseName, response.Payload.Type);
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
        var handler = new PingPongQueryHandler(responseId);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
        
        await registration.DisposeAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        var response = Assert.Single(actual);
        Assert.Equal(ErrorCategory.NoHandlerForQuery.ToString(), response.ErrorCode);
        Assert.Equal($"No handler for query: {handler.RequestName}", response.ErrorMessage.Message);
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
        var handler = new OnePingManyPongQueryHandler(responseIds);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        Assert.Equal(3, actual.Length);
        Assert.Equal(responseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(responseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(responseIds[2].ToString(), actual[2].MessageIdentifier);
    }
    
    [Fact]
    public async Task QueryClosesAfterSomeResponsesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var handler = new OnePingPongForeverQueryHandler();
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var index = 5;
        await foreach (var _ in result)
        {
            if (index-- == 0)
            {
                break;
            }
        }
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.True(handler.Completed);
    }
    
    [Fact]
    public async Task QueryWithManyScatteredResponsesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var firstResponseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var secondResponseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var allResponseIds = firstResponseIds
            .Concat(secondResponseIds)
            .OrderBy(id => id.ToString())
            .ToArray();
        var handler1 = new OnePingManyPongQueryHandler(firstResponseIds);
        var handler2 = new OnePingManyPongQueryHandler(secondResponseIds, handler1.RequestName, handler1.ResponseName);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler1.RequestName), handler1.ResponseName)
        };
        
        await using var registration1 = await sut.RegisterQueryHandlerAsync(
            handler1, 
            queries);
    
        await registration1.WaitUntilCompletedAsync();
        
        await using var registration2 = await sut.RegisterQueryHandlerAsync(
            handler2, 
            queries);
    
        await registration2.WaitUntilCompletedAsync();
    
        var result = sut.Query(new QueryRequest
        {
            Query = handler1.RequestName,
            MessageIdentifier = requestId.ToString()
        }, CancellationToken.None);

        var actual = await result.ToArrayAsync();
        Array.Sort(actual, (left, right) => string.Compare(left.MessageIdentifier, right.MessageIdentifier, StringComparison.Ordinal));
        
        Assert.Equal(3, handler1.ResponsesSent);
        Assert.Equal(3, handler2.ResponsesSent);
        Assert.Equal(6, actual.Length);
        Assert.Equal(allResponseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(allResponseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(allResponseIds[2].ToString(), actual[2].MessageIdentifier);
        Assert.Equal(allResponseIds[3].ToString(), actual[3].MessageIdentifier);
        Assert.Equal(allResponseIds[4].ToString(), actual[4].MessageIdentifier);
        Assert.Equal(allResponseIds[5].ToString(), actual[5].MessageIdentifier);
    }
    
    [Fact]
    public async Task SubscriptionQueryWaitForInitialResultHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseId = InstructionId.New();
        var handler = new PingPongQueryHandler(responseId);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject
        {
            Type = handler.ResponseName,
            Revision = "0",
            Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
        }, new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var actual = await result.InitialResult;
        Assert.Equal(responseId.ToString(), actual.MessageIdentifier);
        Assert.Equal(requestId.ToString(), actual.RequestIdentifier);
        Assert.Equal(handler.ResponseName, actual.Payload.Type);
        Assert.Equal("0", actual.Payload.Revision);
        Assert.Equal(ByteString.CopyFromUtf8("{ \"pong\": true }").ToByteArray(), actual.Payload.Data.ToByteArray());
    }
    
    [Fact]
    public async Task SubscriptionQueryWaitForInitialResultWhenNoHandlerHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;

        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = InstructionId.New().ToString()
        }, new SerializedObject
        {
            Type = "Pong",
            Revision = "0",
            Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
        }, new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AxonServerException>(async () => await result.InitialResult);
        Assert.Equal(ErrorCategory.NoHandlerForQuery, exception.ErrorCategory);
    }
    
    [Fact]
    public async Task SubscriptionQueryWaitForUpdatesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var responseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var handler = new OnePingManyPongQueryHandler(responseIds);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject(), new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var actual = await result.Updates.ToArrayAsync();
        Assert.Equal(3, actual.Length);
        Assert.Equal(responseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(responseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(responseIds[2].ToString(), actual[2].MessageIdentifier);
    }
    
    [Fact]
    public async Task SubscriptionQueryCloseAfterSomeUpdatesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
    
        var requestId = InstructionId.New();
        var handler = new OnePingPongForeverQueryHandler();
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler.RequestName), handler.ResponseName)
        };
        
        await using var registration = await sut.RegisterQueryHandlerAsync(
            handler, 
            queries);
    
        await registration.WaitUntilCompletedAsync();
    
        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = handler.RequestName,
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject(), new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var index = 5;
        await foreach (var _ in result.Updates.WithCancellation(CancellationToken.None))
        {
            if (index-- == 0)
            {
                await result.DisposeAsync();
                break;
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.True(handler.Completed);
    }
    
    [Fact]
    public async Task SubscriptionQueryWaitForUpdatesWhenNoHandlerHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;

        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = "Ping",
            MessageIdentifier = InstructionId.New().ToString()
        }, new SerializedObject(), new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AxonServerException>(async () => await result.Updates.ToArrayAsync());
        Assert.Equal(ErrorCategory.NoHandlerForQuery, exception.ErrorCategory);
    }
    
    [Fact(Skip = "Requires refactoring of the implementation to support response aggregation in case of multiple handlers.")]
    public async Task SubscriptionQueryWaitForScatteredUpdatesHasExpectedResult()
    {
        await using var connection = await CreateSystemUnderTest();
        await connection.WaitUntilConnectedAsync();
        
        var sut = connection.QueryChannel;
        
        var requestId = InstructionId.New();
        var firstResponseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var secondResponseIds = new[] { InstructionId.New(), InstructionId.New(), InstructionId.New() };
        var allResponseIds = firstResponseIds
            .Concat(secondResponseIds)
            .OrderBy(id => id.ToString())
            .ToArray();
        var handler1 = new OnePingManyPongQueryHandler(firstResponseIds);
        var handler2 = new OnePingManyPongQueryHandler(secondResponseIds, handler1.RequestName, handler1.ResponseName);
        var queries = new[]
        {
            new QueryDefinition(new QueryName(handler1.RequestName), handler1.ResponseName)
        };
        
        await using var registration1 = await sut.RegisterQueryHandlerAsync(
            handler1, 
            queries);
        await registration1.WaitUntilCompletedAsync();
        
        await using var registration2 = await sut.RegisterQueryHandlerAsync(
            handler2, 
            queries);
        await registration2.WaitUntilCompletedAsync();

        var result = await sut.SubscriptionQueryAsync(new QueryRequest
        {
            Query = handler1.RequestName,
            MessageIdentifier = requestId.ToString()
        }, new SerializedObject(), new PermitCount(100), new PermitCount(10), CancellationToken.None);

        var actual = await result.Updates.ToArrayAsync();
        Array.Sort(actual, (left, right) => string.Compare(left.MessageIdentifier, right.MessageIdentifier, StringComparison.Ordinal));
        
        Assert.Equal(6, actual.Length);
        Assert.Equal(allResponseIds[0].ToString(), actual[0].MessageIdentifier);
        Assert.Equal(allResponseIds[1].ToString(), actual[1].MessageIdentifier);
        Assert.Equal(allResponseIds[2].ToString(), actual[2].MessageIdentifier);
        Assert.Equal(allResponseIds[3].ToString(), actual[3].MessageIdentifier);
        Assert.Equal(allResponseIds[4].ToString(), actual[4].MessageIdentifier);
        Assert.Equal(allResponseIds[5].ToString(), actual[5].MessageIdentifier);
    }

    private class QueryHandler : IQueryHandler
    {
        public Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel, CancellationToken ct)
        {
            return null;
        }
    }
    
    private class PingPongQueryHandler : IQueryHandler
    {
        private readonly InstructionId _responseId;

        public string RequestName { get; }
        
        public string ResponseName { get; }

        public PingPongQueryHandler(InstructionId responseId)
        {
            var counter = MessageNameCounter.Next();
            RequestName = $"ping{counter}";
            ResponseName = $"pong{counter}";
            _responseId = responseId;
        }
        
        public async Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct)
        {
            if (request.Query == RequestName)
            {
                await responseChannel.SendAsync(new QueryResponse
                {
                    MessageIdentifier = _responseId.ToString(),
                    RequestIdentifier = request.MessageIdentifier,
                    Payload = new SerializedObject
                    {
                        Type = ResponseName,
                        Revision = "0",
                        Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                    }
                }, ct);
                await responseChannel.CompleteAsync(ct);
            }
        }

        public Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel, CancellationToken ct)
        {
            return query.QueryRequest.Query == RequestName ? TryHandleAsyncCore(responseChannel, ct) : null;
        }

        private async Task TryHandleAsyncCore(
            ISubscriptionQueryUpdateResponseChannel responseChannel, 
            CancellationToken ct)
        {
            await responseChannel.SendUpdateAsync(new QueryUpdate
            {
                MessageIdentifier = _responseId.ToString(),
                Payload = new SerializedObject
                {
                    Type = ResponseName,
                    Revision = "0",
                    Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                }
            }, ct);
            
            await responseChannel.CompleteAsync(ct);
        }
    }
    
    private class OnePingManyPongQueryHandler : IQueryHandler
    {
        private readonly InstructionId[] _responseIds;
        
        public string RequestName { get; }
        
        public string ResponseName { get; }

        public OnePingManyPongQueryHandler(InstructionId[] responseIds)
        {
            var counter = MessageNameCounter.Next();
            RequestName = $"ping{counter}";
            ResponseName = $"pong{counter}";
            _responseIds = responseIds;
        }
        
        public OnePingManyPongQueryHandler(InstructionId[] responseIds, string requestName, string responseName)
        {
            RequestName = requestName;
            ResponseName = responseName;
            _responseIds = responseIds;
        }

        public int ResponsesSent { get; private set; }

        public async Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct)
        {
            if (request.Query == RequestName)
            {
                foreach (var responseId in _responseIds)
                {
                    await responseChannel.SendAsync(new QueryResponse
                    {
                        MessageIdentifier = responseId.ToString(),
                        RequestIdentifier = request.MessageIdentifier,
                        Payload = new SerializedObject
                        {
                            Type = ResponseName,
                            Revision = "0",
                            Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                        }
                    }, ct);
                    ResponsesSent++;
                }

                await responseChannel.CompleteAsync(ct);
            }
        }
        
        public Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel, CancellationToken ct)
        {
            return query.QueryRequest.Query == RequestName ? TryHandleAsyncCore(responseChannel, ct) : null;
        }

        private async Task TryHandleAsyncCore(
            ISubscriptionQueryUpdateResponseChannel responseChannel, 
            CancellationToken ct)
        {
            foreach (var responseId in _responseIds)
            {
                await responseChannel.SendUpdateAsync(new QueryUpdate
                {
                    MessageIdentifier = responseId.ToString(),
                    Payload = new SerializedObject
                    {
                        Type = ResponseName,
                        Revision = "0",
                        Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                    }
                }, ct);
                ResponsesSent++;
            }
            
            await responseChannel.CompleteAsync(ct);
        }
    }
    
    private class OnePingPongForeverQueryHandler : IQueryHandler
    {
        public string RequestName { get; }
        
        public string ResponseName { get; }
        
        public OnePingPongForeverQueryHandler()
        {
            var counter = MessageNameCounter.Next();
            RequestName = $"ping{counter}";
            ResponseName = $"pong{counter}";
        }

        public bool Completed { get; private set; }
        
        public async Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct)
        {
            if (request.Query == RequestName)
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await responseChannel.SendAsync(new QueryResponse
                        {
                            MessageIdentifier = InstructionId.New().ToString(),
                            RequestIdentifier = request.MessageIdentifier,
                            Payload = new SerializedObject
                            {
                                Type = ResponseName,
                                Revision = "0",
                                Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                            }
                        }, ct);

                        await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                    }
                }
                finally
                {
                    Completed = true;
                }
            }
        }
        
        public Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel, CancellationToken ct)
        {
            return query.QueryRequest.Query == RequestName ? TryHandleAsyncCore(responseChannel, ct) : null;
        }

        private async Task TryHandleAsyncCore(
            ISubscriptionQueryUpdateResponseChannel responseChannel, 
            CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await responseChannel.SendUpdateAsync(new QueryUpdate
                    {
                        MessageIdentifier = InstructionId.New().ToString(),
                        Payload = new SerializedObject
                        {
                            Type = ResponseName,
                            Revision = "0",
                            Data = ByteString.CopyFromUtf8("{ \"pong\": true }")
                        }
                    }, ct);

                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                }
            }
            finally
            {
                Completed = true;
            }
        }
    }

    private static class MessageNameCounter
    {
        private static int _counter;

        public static int Next()
        {
            return Interlocked.Increment(ref _counter);
        }
    }
}