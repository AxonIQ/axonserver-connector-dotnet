using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal abstract record QueryReply
{
    public record Send(ChannelId Id, QueryResponse Response) : QueryReply;

    public record Complete(ChannelId Id) : QueryReply;

    public record CompleteWithError(ChannelId Id, ErrorMessage Error) : QueryReply;
}