using AxonIQ.AxonServer.Grpc;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask ReceiveHeartbeatAcknowledgement(InstructionAck message);