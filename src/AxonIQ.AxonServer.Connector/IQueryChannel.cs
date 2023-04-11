using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryChannel
{
    Task<IQueryHandlerRegistration> RegisterQueryHandlerAsync(
        IQueryHandler handler,
        params QueryDefinition[] queries);

    IAsyncEnumerable<QueryResponse> Query(QueryRequest query, CancellationToken ct);
    
    Task<IQuerySubscriptionResult> SubscriptionQueryAsync(
        QueryRequest query, 
        SerializedObject updateType,
        PermitCount bufferSize,
        PermitCount fetchSize,
        CancellationToken ct);
}