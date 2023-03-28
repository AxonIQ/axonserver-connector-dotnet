using Io.Axoniq.Axonserver.Grpc.Control;

namespace AxonIQ.AxonServer.Connector;

public interface IControlChannel
{
    Task EnableHeartbeat(TimeSpan interval, TimeSpan timeout);
    Task DisableHeartbeat();
    
    Task SendInstruction(PlatformInboundInstruction instruction);

    Task<IEventProcessorRegistration> RegisterEventProcessor(
        EventProcessorName name,
        Func<Task<EventProcessorInfo?>> supplier,
        IEventProcessorInstructionHandler handler);
}