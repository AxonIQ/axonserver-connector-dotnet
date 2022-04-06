using AxonIQ.AxonServer.Grpc.Command;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask WriteCommandProviderOutbound(CommandProviderOutbound instruction);