using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryResponseChannel
{
    ValueTask SendAsync(QueryResponse response, CancellationToken cancellationToken);
    async ValueTask SendLastAsync(QueryResponse response, CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    ValueTask CompleteAsync(CancellationToken cancellationToken);
    ValueTask CompleteWithErrorAsync(ErrorMessage error, CancellationToken cancellationToken);

    ValueTask CompleteWithErrorAsync(ErrorCategory category, string message, CancellationToken cancellationToken) =>
        CompleteWithErrorAsync(new ErrorMessage
        {
            ErrorCode = category.ToString(),
            Message = message
        }, cancellationToken);
}