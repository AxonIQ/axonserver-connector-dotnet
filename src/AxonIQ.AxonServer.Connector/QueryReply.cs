using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

internal abstract record QueryReply
{
    public record Send(QueryResponse Response) : QueryReply;

    public record Complete : QueryReply;

    public record CompleteWithError(ErrorMessage Error) : QueryReply;
}