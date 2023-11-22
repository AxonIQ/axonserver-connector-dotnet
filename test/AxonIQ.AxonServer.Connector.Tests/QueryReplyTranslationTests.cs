using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector.Tests;

public class QueryReplyTranslationTests
{
    private readonly InstructionId _instructionId;
    private readonly QueryReplyTranslator _sut;

    public QueryReplyTranslationTests()
    {
        _instructionId = InstructionId.New();
        _sut = QueryReplyTranslation.ForQuery(new QueryRequest
            {
                MessageIdentifier = _instructionId.ToString()
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
        Assert.NotEmpty(message.QueryResponse.MessageIdentifier);
        Assert.NotEmpty(message.InstructionId);
        Assert.Equal(_instructionId.ToString(), message.QueryResponse.RequestIdentifier);
    }
    
    [Fact]
    public void TranslationOfSendHasExpectedResultWhenMessageIdentifierProvided()
    {
        var instructionId = InstructionId.New();
        var payload = new SerializedObject();
        var actual = _sut(new QueryReply.Send(new QueryResponse
        {
            MessageIdentifier = instructionId.ToString(),
            Payload = payload
        }));

        Assert.Equal(new []
        {
            new QueryProviderOutbound
            {
                QueryResponse = new QueryResponse
                {
                    MessageIdentifier = instructionId.ToString(),
                    Payload = payload,
                    RequestIdentifier = _instructionId.ToString(),
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