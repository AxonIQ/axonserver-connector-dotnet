using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class SubscriptionQueryUpdateResponseChannel : ISubscriptionQueryUpdateResponseChannel
{
    private readonly ClientIdentity _clientIdentity;
    private readonly SubscriptionId _subscriptionId;
    private readonly WriteQueryProviderOutbound _writer;

    public SubscriptionQueryUpdateResponseChannel(ClientIdentity clientIdentity, SubscriptionId subscriptionId, WriteQueryProviderOutbound writer)
    {
        _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        _subscriptionId = subscriptionId;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
    
    public ValueTask SendUpdateAsync(QueryUpdate update, CancellationToken ct)
    {
        return _writer(new QueryProviderOutbound
        {
            SubscriptionQueryResponse = new SubscriptionQueryResponse
            {
                SubscriptionIdentifier = _subscriptionId.ToString(),
                Update = update,
                MessageIdentifier = update.MessageIdentifier
            }
        });
    }

    public ValueTask CompleteAsync(CancellationToken ct)
    {
        return _writer(new QueryProviderOutbound
        {
            SubscriptionQueryResponse = new SubscriptionQueryResponse
            {
                SubscriptionIdentifier = _subscriptionId.ToString(),
                Complete = new QueryUpdateComplete
                {
                    ClientId = _clientIdentity.ClientInstanceId.ToString(),
                    ComponentName = _clientIdentity.ComponentName.ToString()
                }
            }
        });
    }
}