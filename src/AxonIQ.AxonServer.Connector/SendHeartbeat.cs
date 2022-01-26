namespace AxonIQ.AxonServer.Connector;

public delegate ValueTask SendHeartbeat(ReceiveHeartbeatAcknowledgement responder, TimeSpan timeout);