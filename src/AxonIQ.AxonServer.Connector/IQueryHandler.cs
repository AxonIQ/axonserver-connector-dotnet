using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryHandler
{
    Task HandleAsync(QueryRequest request, IQueryResponseChannel responseChannel, CancellationToken ct);

    Task? TryHandleAsync(SubscriptionQuery query, ISubscriptionQueryUpdateResponseChannel responseChannel, CancellationToken ct);
}