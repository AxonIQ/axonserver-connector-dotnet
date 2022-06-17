using Io.Axoniq.Axonserver.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask WriteCommandProviderOutbound(CommandProviderOutbound instruction);