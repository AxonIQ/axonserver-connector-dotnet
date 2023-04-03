using AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;
using AxonIQ.AxonServer.Embedded;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Io.Axoniq.Axonserver.Grpc.Control;
using Xunit;

namespace AxonIQ.AxonServer.Connector.IntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class HeartbeatSanityCheckTests
{
    private readonly IAxonServer _container;

    public HeartbeatSanityCheckTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }
    
    [Fact]
    public async Task HeartbeatGetsAcknowledged()
    {
        var channel = _container.CreateGrpcChannel(null);
        var callInvoker = channel.Intercept(metadata =>
        {
            Context.Default.WriteTo(metadata);
            return metadata;
        });
        var service = new PlatformService.PlatformServiceClient(callInvoker);
        var stream = service.OpenStream();
        await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        {
            InstructionId = InstructionId.New().ToString(),
            Register = new ClientIdentification
            {
                ClientId = "1234",
                ComponentName = "789",
                Version = "1.0"
            }
        });
        var instructionId = InstructionId.New().ToString();
        await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        {
            InstructionId = instructionId,
            Heartbeat = new Heartbeat()
        });
        await foreach (var instruction in stream.ResponseStream.ReadAllAsync())
        {
            if (instruction.RequestCase == PlatformOutboundInstruction.RequestOneofCase.Ack
                && instruction.Ack.InstructionId == instructionId)
            {
                break;
            }
        }
    }
}

//Why? Because writes to the request stream need to be serialized