using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal class BufferedQueryResponseChannel : IQueryResponseChannel
{
    private readonly Channel<QueryReply> _channel;

    public BufferedQueryResponseChannel(Channel<QueryReply> channel)
    {
        _channel = channel;
    }
    
    public ValueTask SendAsync(QueryResponse response, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(new QueryReply.Send(response), cancellationToken);
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(new QueryReply.Complete(), cancellationToken);
        _channel.Writer.Complete();
    }

    public async ValueTask CompleteWithErrorAsync(ErrorMessage error, CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(new QueryReply.CompleteWithError(error), cancellationToken);
        _channel.Writer.Complete();
    }
}