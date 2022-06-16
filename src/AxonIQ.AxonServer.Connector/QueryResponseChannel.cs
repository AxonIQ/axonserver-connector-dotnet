using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public class QueryResponseChannel : IQueryResponseChannel
{
    private readonly QueryRequest _request;
    private readonly WriteQueryProviderOutbound _writer;

    public QueryResponseChannel(QueryRequest request, WriteQueryProviderOutbound writer)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
        
    public ValueTask WriteAsync(QueryResponse response)
    {
        return _writer(new QueryProviderOutbound
        {
            QueryResponse = response,
            InstructionId = InstructionId.New().ToString()
        });
    }

    public ValueTask CompleteAsync()
    {
        var instructionId = InstructionId.New().ToString();
        return _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId
            },
            InstructionId = instructionId
        });
    }

    public async ValueTask CompleteWithErrorAsync(ErrorMessage errorMessage)
    {
        var instructionId1 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryResponse = new QueryResponse
            {
                ErrorMessage = errorMessage,
                MessageIdentifier = instructionId1
            },
            InstructionId = instructionId1
        });
        
        var instructionId2 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId2
            },
            InstructionId = instructionId2
        });
    }

    public async ValueTask CompleteWithErrorAsync(ErrorCategory errorCategory, ErrorMessage errorMessage)
    {
        var instructionId1 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryResponse = new QueryResponse
            {
                ErrorMessage = errorMessage,
                ErrorCode = errorCategory.ToString(),
                MessageIdentifier = instructionId1
            },
            InstructionId = instructionId1
        });
        
        var instructionId2 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId2
            },
            InstructionId = instructionId2
        });
    }
}