using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public interface IQueryHandler
{
    Task Handle(QueryRequest request, IQueryResponseChannel responseChannel);
}