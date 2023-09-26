using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryHandler
{
    Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct);

    ISubscriptionQueryRegistration? RegisterSubscriptionQuery(
        SubscriptionQuery query,
        ISubscriptionQueryUpdateResponseChannel responseChannel);
}