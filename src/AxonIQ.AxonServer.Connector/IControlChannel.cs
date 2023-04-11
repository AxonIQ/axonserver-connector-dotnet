using Io.Axoniq.Axonserver.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

public interface IControlChannel
{
    Task EnableHeartbeatAsync(TimeSpan interval, TimeSpan timeout);
    Task DisableHeartbeatAsync();
    
    Task SendInstructionAsync(PlatformInboundInstruction instruction);

    Task<IEventProcessorRegistration> RegisterEventProcessorAsync(
        EventProcessorName name,
        Func<Task<EventProcessorInfo?>> supplier,
        IEventProcessorInstructionHandler handler);
}