using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQuerySubscriptionResult : IAsyncDisposable
{
    Task<QueryResponse> InitialResult { get; }
    IAsyncEnumerable<QueryUpdate> Updates { get; }
}