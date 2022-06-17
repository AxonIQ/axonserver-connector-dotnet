using Io.Axoniq.Axonserver.Grpc.Query;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask WriteQueryProviderOutbound(QueryProviderOutbound instruction);