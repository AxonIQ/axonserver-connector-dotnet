using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryChannel
{
    IAsyncEnumerable<IQueryHandlerRegistration> RegisterQueryHandler(
        Func<QueryRequest, CancellationToken, Task<IAsyncEnumerable<QueryResponse>>> handler,
        params QueryDefinition[] queries);

    IAsyncEnumerable<QueryResponse> Query(QueryRequest request);
    
    IAsyncEnumerable<QueryResponse> SubscribeToQuery(
        QueryRequest request, 
        SerializedObject updateType,
        int bufferSize,
        int fetchSize);
}