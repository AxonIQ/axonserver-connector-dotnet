using AutoFixture;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector.Tests;

public class QueryHandlerCollectionTests
{
    private readonly Fixture _fixture;

    public QueryHandlerCollectionTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeRegisteredQueryId();
    }

    private QueryHandlerCollection CreateSystemUnderTest(Func<DateTimeOffset>? clock = default)
    {
        return new QueryHandlerCollection(
            _fixture.Create<ClientIdentity>(),
            clock ?? (() => DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void RegisterNonExistingQueryHandlerHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id, definition, handler);
        
        Assert.Equal(new[] { handler }, sut.ResolveQueryHandlers(queryName));
        Assert.Equal(1, sut.RegisteredQueryCount);
        Assert.True(sut.HasRegisteredQueries);
    }
    
    [Fact]
    public void RegisterExistingQueryHandlerHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id, definition, handler);
        sut.RegisterQueryHandler(id, definition, handler);
        
        Assert.Equal(new[] { handler }, sut.ResolveQueryHandlers(queryName));
        Assert.Equal(1, sut.RegisteredQueryCount);
        Assert.True(sut.HasRegisteredQueries);
    }
    
    [Fact]
    public void RegisterQueryHandlersForSameQueryNameHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.Equal(new IQueryHandler[] { handler, handler }, sut.ResolveQueryHandlers(queryName));
        Assert.Equal(2, sut.RegisteredQueryCount);
        Assert.True(sut.HasRegisteredQueries);
    }
    
    [Fact]
    public void TryBeginSubscribeToQueryInstructionHasExpectedResultWhenNotSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.NotNull(sut.TryBeginSubscribeToQueryInstruction(definition));
        Assert.Null(sut.TryBeginSubscribeToQueryInstruction(definition));
    }
    
    [Fact]
    public void TryBeginSubscribeToQueryInstructionHasExpectedResultWhenSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));

        Assert.Null(sut.TryBeginSubscribeToQueryInstruction(definition));
    }
    
    [Fact]
    public void BeginSubscribeToAllInstructionsHasExpectedResultWhenSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));

        Assert.Single(sut.BeginSubscribeToAllInstructions());
    }
    
    [Fact]
    public void BeginSubscribeToAllInstructionsHasExpectedResultWhenNotSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);

        var actual = Assert.Single(sut.BeginSubscribeToAllInstructions());
        Assert.Equal(queryName.ToString(), actual.Subscribe.Query);
    }
    
    [Fact]
    public void BeginSubscribeToAllInstructionsHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName1 = _fixture.Create<QueryName>();
        var queryName2 = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition1 = new QueryDefinition(queryName1, resultName);
        var definition2 = new QueryDefinition(queryName2, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition1, handler);
        sut.RegisterQueryHandler(id2, definition2, handler);

        var actual = sut.BeginSubscribeToAllInstructions();
        
        Assert.Equal(2, actual.Count);

        Assert.Contains(actual, entry => entry.Subscribe.Query.Equals(queryName1.ToString()));
        Assert.Contains(actual, entry => entry.Subscribe.Query.Equals(queryName2.ToString()));
    }
    
    [Fact]
    public void RegisterSubscribeToQueryCompletionSourceHasExpectedResultOnSuccessAck()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();
        var completionSource = new TaskCompletionSource();

        sut.RegisterQueryHandler(id, definition, handler);
        sut.RegisterSubscribeToQueryCompletionSource(definition, completionSource);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));
        
        Assert.True(completionSource.Task.IsCompletedSuccessfully);
    }
    
    [Fact]
    public void RegisterSubscribeToQueryCompletionSourceHasExpectedResultOnFailureAck()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();
        var completionSource = new TaskCompletionSource();

        sut.RegisterQueryHandler(id, definition, handler);
        sut.RegisterSubscribeToQueryCompletionSource(definition, completionSource);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = false,
            Error = new ErrorMessage
            {
                Message = "Failed to subscribe to query"
            }
        }));
        
        Assert.True(completionSource.Task.IsFaulted);
        var outerException = Assert.IsType<AggregateException>(completionSource.Task.Exception);
        var innerException = Assert.IsType<AxonServerException>(outerException.InnerException);
        Assert.Equal("Failed to subscribe to query", innerException.Message);
    }

    [Fact]
    public void ResolveQueryHandlersHasExpectedResultWhenSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));

        var handlers = sut.ResolveQueryHandlers(queryName);
        
        var actual = Assert.Single(handlers);
        Assert.Same(handler, actual);
    }
    
    [Fact]
    public void ResolveQueryHandlersHasExpectedResultWhenNotSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id, definition, handler);

        var handlers = sut.ResolveQueryHandlers(queryName);
        
        var actual = Assert.Single(handlers);
        Assert.Same(handler, actual);
    }
    
    [Fact]
    public void ResolveQueryHandlersHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var handlers = sut.ResolveQueryHandlers(_fixture.Create<QueryName>());
        
        Assert.Empty(handlers);
    }
    
    [Fact]
    public void UnregisterExistingQueryHandlerHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id, definition, handler);
        sut.UnregisterQueryHandler(id);
        
        Assert.Empty(sut.ResolveQueryHandlers(queryName));
        Assert.Equal(0, sut.RegisteredQueryCount);
        Assert.False(sut.HasRegisteredQueries);
    }
    
    [Fact]
    public void UnregisterNonExistingQueryHandlerHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();

        sut.UnregisterQueryHandler(id);
        
        Assert.Empty(sut.ResolveQueryHandlers(queryName));
        Assert.Equal(0, sut.RegisteredQueryCount);
        Assert.False(sut.HasRegisteredQueries);
    }
    
    [Fact]
    public void TryBeginUnsubscribeFromQueryInstructionHasExpectedResultWhenNotSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.Null(sut.TryBeginUnsubscribeFromQueryInstruction(definition));
        Assert.Null(sut.TryBeginUnsubscribeFromQueryInstruction(definition));
    }
    
    [Fact]
    public void TryBeginUnsubscribeFromQueryInstructionHasExpectedResultWhenSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));
        Assert.Null(sut.TryBeginSubscribeToQueryInstruction(definition));

        Assert.Null(sut.TryBeginUnsubscribeFromQueryInstruction(definition));
        Assert.NotNull(sut.TryBeginUnsubscribeFromQueryInstruction(definition));
    }
    
    [Fact]
    public void BeginUnsubscribeFromAllInstructionsHasExpectedResultWhenSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));

        Assert.Single(sut.BeginUnsubscribeFromAllInstructions());
    }
    
    [Fact]
    public void BeginUnsubscribeFromAllInstructionsHasExpectedResultWhenNotSubscribed()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition, handler);
        sut.RegisterQueryHandler(id2, definition, handler);

        var actual = Assert.Single(sut.BeginUnsubscribeFromAllInstructions());
        Assert.Equal(queryName.ToString(), actual.Unsubscribe.Query);
    }
    
    [Fact]
    public void BeginUnsubscribeFromAllInstructionsHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredQueryId>();
        var id2 = _fixture.Create<RegisteredQueryId>();
        var queryName1 = _fixture.Create<QueryName>();
        var queryName2 = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition1 = new QueryDefinition(queryName1, resultName);
        var definition2 = new QueryDefinition(queryName2, resultName);
        var handler = new EmptyHandler();

        sut.RegisterQueryHandler(id1, definition1, handler);
        sut.RegisterQueryHandler(id2, definition2, handler);

        var actual = sut.BeginUnsubscribeFromAllInstructions();
        
        Assert.Equal(2, actual.Count);

        Assert.Contains(actual, entry => entry.Unsubscribe.Query.Equals(queryName1.ToString()));
        Assert.Contains(actual, entry => entry.Unsubscribe.Query.Equals(queryName2.ToString()));
    }
    
    [Fact]
    public void RegisterUnsubscribeFromQueryCompletionSourceHasExpectedResultOnSuccessAck()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();
        var completionSource = new TaskCompletionSource();

        sut.RegisterQueryHandler(id, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));
        
        sut.UnregisterQueryHandler(id);
        sut.RegisterUnsubscribeFromQueryCompletionSource(definition, completionSource);
        
        Assert.True(sut.TryCompleteUnsubscribeFromQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginUnsubscribeFromQueryInstruction(definition)!.InstructionId,
            Success = true
        }));
        
        Assert.True(completionSource.Task.IsCompletedSuccessfully);
    }
    
    [Fact]
    public void RegisterUnsubscribeFromQueryCompletionSourceHasExpectedResultOnFailureAck()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredQueryId>();
        var queryName = _fixture.Create<QueryName>();
        var resultName = _fixture.Create<string>();
        var definition = new QueryDefinition(queryName, resultName);
        var handler = new EmptyHandler();
        var completionSource = new TaskCompletionSource();

        sut.RegisterQueryHandler(id, definition, handler);
        
        Assert.True(sut.TryCompleteSubscribeToQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginSubscribeToQueryInstruction(definition)!.InstructionId,
            Success = true
        }));
        
        sut.UnregisterQueryHandler(id);
        sut.RegisterUnsubscribeFromQueryCompletionSource(definition, completionSource);
        
        Assert.True(sut.TryCompleteUnsubscribeFromQueryInstruction(new InstructionAck
        {
            InstructionId = sut.TryBeginUnsubscribeFromQueryInstruction(definition)!.InstructionId,
            Success = false,
            Error = new ErrorMessage
            {
                Message = "Failed to unsubscribe from query"
            }
        }));
        
        Assert.True(completionSource.Task.IsFaulted);
        var outerException = Assert.IsType<AggregateException>(completionSource.Task.Exception);
        var innerException = Assert.IsType<AxonServerException>(outerException.InnerException);
        Assert.Equal("Failed to unsubscribe from query", innerException.Message);
    }

    private class EmptyHandler : IQueryHandler
    {
        public Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task HandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel,
            CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel,
            CancellationToken ct)
        {
            return null;
        }
    }
}

