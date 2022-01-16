using AxonIQ.AxonServer.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

public interface IControlChannel
{
    Task EnableHeartbeat(TimeSpan interval, TimeSpan timeout);
    Task DisableHeartbeat();
    Task SendInstruction(PlatformInboundInstruction instruction);
}