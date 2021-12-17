using AxonIQ.AxonServer.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

public delegate Task WritePlatformOutboundInstruction(PlatformOutboundInstruction instruction);