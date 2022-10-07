using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface ISubscriptionQueryUpdateResponseChannel
{
    ValueTask SendUpdateAsync(QueryUpdate update);
    ValueTask CompleteAsync();
}