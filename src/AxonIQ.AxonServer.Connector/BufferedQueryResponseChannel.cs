using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class BufferedQueryResponseChannel : IQueryResponseChannel
{
    private readonly Channel<QueryReply> _channel;
    private readonly ILogger _logger;

    public BufferedQueryResponseChannel(Channel<QueryReply> channel, ILogger logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public ValueTask SendAsync(QueryResponse response, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sending query response: {Response}", response);
        return _channel.Writer.WriteAsync(new QueryReply.Send(response), cancellationToken);
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Completing query");
        await _channel.Writer.WriteAsync(new QueryReply.Complete(), cancellationToken);
        _channel.Writer.Complete();
    }

    public async ValueTask CompleteWithErrorAsync(ErrorMessage error, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Completing query with {Error}", error);
        await _channel.Writer.WriteAsync(new QueryReply.CompleteWithError(error), cancellationToken);
        _channel.Writer.Complete();
    }
}