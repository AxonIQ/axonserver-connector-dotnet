using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryResponseChannel
{
    ValueTask SendAsync(QueryResponse response);
    async ValueTask SendLastAsync(QueryResponse response)
    {
        try
        {
            await SendAsync(response);
        }
        finally
        {
            await CompleteAsync();
        }
    }
    ValueTask CompleteAsync();
    ValueTask CompleteWithErrorAsync(ErrorMessage errorMessage);
    ValueTask CompleteWithErrorAsync(ErrorCategory errorCategory, ErrorMessage errorMessage);
}