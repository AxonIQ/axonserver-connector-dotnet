using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class BufferedQueryResponseChannel(ChannelId id, Channel<QueryReply> channel, ILogger logger) : IQueryResponseChannel
{
    private long _completed = Completed.No;

    public ValueTask SendAsync(QueryResponse response, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending query response on channel {ChannelId}: {Response}", id.ToString(), response);
        return channel.Writer.WriteAsync(new QueryReply.Send(id, response), cancellationToken);
    }

    public ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        if(Interlocked.CompareExchange(ref _completed, Completed.Yes, Completed.No) == Completed.No)
        {
            logger.LogDebug("Completing query response channel {ChannelId}", id.ToString());
            return channel.Writer.WriteAsync(new QueryReply.Complete(id), cancellationToken);
        }
        logger.LogDebug("Query response channel {ChannelId} already completed", id.ToString());
        return ValueTask.CompletedTask;
    }

    public ValueTask CompleteWithErrorAsync(ErrorMessage error, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _completed, Completed.Yes, Completed.No) == Completed.No)
        {
            logger.LogDebug("Completing query response channel {ChannelId} with {Error}", id.ToString(), error);
            return channel.Writer.WriteAsync(new QueryReply.CompleteWithError(id, error), cancellationToken);
        }
        logger.LogDebug("Query response channel {ChannelId} already completed", id.ToString());
        return ValueTask.CompletedTask;
    }
}