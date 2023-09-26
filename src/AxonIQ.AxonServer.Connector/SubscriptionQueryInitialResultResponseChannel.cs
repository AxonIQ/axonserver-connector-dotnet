using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class SubscriptionQueryInitialResultResponseChannel : IQueryResponseChannel
{
    private readonly SubscriptionIdentifier _subscriptionIdentifier;
    private readonly QueryRequest _request;
    private readonly WriteQueryProviderOutbound _writer;

    public SubscriptionQueryInitialResultResponseChannel(SubscriptionIdentifier subscriptionIdentifier, QueryRequest request, WriteQueryProviderOutbound writer)
    {
        _subscriptionIdentifier = subscriptionIdentifier;
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
        
    public ValueTask SendAsync(QueryResponse response, CancellationToken cancellationToken)
    {
        return _writer(new QueryProviderOutbound
        {
            SubscriptionQueryResponse = new SubscriptionQueryResponse
            {
                SubscriptionIdentifier = _subscriptionIdentifier.ToString(),
                InitialResult = response,
                MessageIdentifier = response.MessageIdentifier
            }
        });
    }

    public ValueTask CompleteAsync(CancellationToken cancellationToken)
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

    public async ValueTask CompleteWithErrorAsync(ErrorMessage error, CancellationToken cancellationToken)
    {
        var instructionId1 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryResponse = new QueryResponse
            {
                ErrorMessage = error,
                MessageIdentifier = instructionId1
            },
            InstructionId = instructionId1
        }).ConfigureAwait(false);
        
        var instructionId2 = InstructionId.New().ToString();
        await _writer(new QueryProviderOutbound
        {
            QueryComplete = new QueryComplete
            {
                RequestId = _request.MessageIdentifier,
                MessageId = instructionId2
            },
            InstructionId = instructionId2
        }).ConfigureAwait(false);
    }
}