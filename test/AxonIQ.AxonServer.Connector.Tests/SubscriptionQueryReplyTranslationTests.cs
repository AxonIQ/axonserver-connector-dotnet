using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector.Tests;

public class SubscriptionQueryReplyTranslationTests
{
    private readonly SubscriptionId _subscriptionId;
    private readonly InstructionId _instructionId;
    private readonly QueryReplyTranslator _sut;

    public SubscriptionQueryReplyTranslationTests()
    {
        _subscriptionId = SubscriptionId.New();
        _instructionId = InstructionId.New();
        _sut = QueryReplyTranslation.ForSubscriptionQuery(new SubscriptionQuery
            {
                SubscriptionIdentifier = _subscriptionId.ToString(),
                QueryRequest = new QueryRequest { MessageIdentifier = _instructionId.ToString() }
            }
        );
    }

    [Fact]
    public void TranslationOfSendHasExpectedResultWhenMessageIdentifierNotProvided()
    {
        var payload = new SerializedObject();
        var actual = _sut(new QueryReply.Send(new QueryResponse
        {
            Payload = payload
        }));

        var message = Assert.Single(actual);
        Assert.NotEmpty(message.SubscriptionQueryResponse.MessageIdentifier);
        Assert.NotEmpty(message.InstructionId);
        Assert.Equal(_subscriptionId.ToString(), message.SubscriptionQueryResponse.SubscriptionIdentifier);
    }
    
    [Fact]
    public void TranslationOfSendHasExpectedResultWhenMessageIdentifierProvided()
    {
        var instructionId = InstructionId.New();
        var payload = new SerializedObject();
        var queryResponse = new QueryResponse
        {
            MessageIdentifier = instructionId.ToString(),
            Payload = payload
        };
        var actual = _sut(new QueryReply.Send(queryResponse));

        Assert.Equal(new []
        {
            new QueryProviderOutbound
            {
                SubscriptionQueryResponse = new SubscriptionQueryResponse
                {
                    SubscriptionIdentifier = _subscriptionId.ToString(),
                    InitialResult = queryResponse,
                    MessageIdentifier = instructionId.ToString(),
                },
                InstructionId = instructionId.ToString()
                
            }
        }, actual);
    }

    [Fact]
    public void TranslationOfCompleteHasExpectedResult()
    {
        var actual = _sut(new QueryReply.Complete());

        var message = Assert.Single(actual);
        Assert.Equal(_instructionId.ToString(), message.QueryComplete.RequestId);
        Assert.NotEmpty(message.QueryComplete.MessageId);
    }

    [Fact]
    public void TranslationOfCompleteWithErrorHasExpectedResult()
    {
        var error = new ErrorMessage();
        
        var actual = _sut(new QueryReply.CompleteWithError(error)).ToList();
        
        Assert.Equal(2, actual.Count);
        var response = actual[0];
        Assert.Equal(response.QueryResponse.ErrorMessage, error);
        Assert.NotEmpty(response.InstructionId);
        Assert.Equal(_instructionId.ToString(), response.QueryResponse.RequestIdentifier);
        var complete = actual[1];
        Assert.Equal(_instructionId.ToString(), complete.QueryComplete.RequestId);
        Assert.NotEmpty(complete.QueryComplete.MessageId);
    }
}