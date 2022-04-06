using AxonIQ.AxonServer.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask WritePlatformInboundInstruction(PlatformInboundInstruction instruction);