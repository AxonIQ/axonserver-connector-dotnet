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