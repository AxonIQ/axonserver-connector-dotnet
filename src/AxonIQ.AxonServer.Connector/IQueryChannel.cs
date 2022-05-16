using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryResponseChannel
{
    ValueTask WriteAsync(QueryResponse response);
    ValueTask CompleteAsync();
    ValueTask CompleteWithErrorAsync(ErrorMessage errorMessage);
    ValueTask CompleteWithErrorAsync(ErrorCategory errorCategory, ErrorMessage errorMessage);
}

public interface IQueryHandler
{
    Task Handle(QueryRequest request, IQueryResponseChannel responseChannel);
}

public interface IQueryChannel
{
    Task<IQueryHandlerRegistration> RegisterQueryHandler(
        IQueryHandler handler,
        params QueryDefinition[] queries);

    IAsyncEnumerable<QueryResponse> Query(QueryRequest query, CancellationToken ct);
    
    Task<IQuerySubscriptionResult> SubscribeToQuery(
        QueryRequest query, 
        SerializedObject updateType,
        PermitCount bufferSize,
        PermitCount fetchSize,
        CancellationToken ct);
}