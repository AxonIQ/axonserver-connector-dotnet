using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class SubscriptionQueryUpdateResponseChannel : ISubscriptionQueryUpdateResponseChannel
{
    private readonly ClientIdentity _clientIdentity;
    private readonly SubscriptionIdentifier _subscriptionIdentifier;
    private readonly WriteQueryProviderOutbound _writer;

    public SubscriptionQueryUpdateResponseChannel(ClientIdentity clientIdentity, SubscriptionIdentifier subscriptionIdentifier, WriteQueryProviderOutbound writer)
    {
        _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        _subscriptionIdentifier = subscriptionIdentifier;
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }
    
    public ValueTask SendUpdateAsync(QueryUpdate update)
    {
        return _writer(new QueryProviderOutbound
        {
            SubscriptionQueryResponse = new SubscriptionQueryResponse
            {
                SubscriptionIdentifier = _subscriptionIdentifier.ToString(),
                Update = update,
                MessageIdentifier = update.MessageIdentifier
            }
        });
    }

    public ValueTask CompleteAsync()
    {
        return _writer(new QueryProviderOutbound
        {
            SubscriptionQueryResponse = new SubscriptionQueryResponse
            {
                SubscriptionIdentifier = _subscriptionIdentifier.ToString(),
                Complete = new QueryUpdateComplete
                {
                    ClientId = _clientIdentity.ClientInstanceId.ToString(),
                    ComponentName = _clientIdentity.ComponentName.ToString()
                }
            }
        });
    }
}