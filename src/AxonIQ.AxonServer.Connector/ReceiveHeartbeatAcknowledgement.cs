using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask ReceiveHeartbeatAcknowledgement(InstructionAck message);