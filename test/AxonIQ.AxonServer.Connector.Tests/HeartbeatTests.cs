using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Grpc.Control;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class HeartbeatTests
{
    private readonly IAxonServer _container;

    public HeartbeatTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }
    
    [Fact]
    public async Task HeartbeatGetsAcknowledged()
    {
        var channel = _container.CreateGrpcChannel(null);
        var context = Context.Default;
        var callInvoker = channel.Intercept(metadata =>
        {
            context.WriteTo(metadata);
            return metadata;
        });
        var service = new PlatformService.PlatformServiceClient(callInvoker);
        var stream = service.OpenStream();
        await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        {
            InstructionId = Guid.NewGuid().ToString("N"),
            Register = new ClientIdentification
            {
                ClientId = "1234",
                ComponentName = "789",
                Version = "1.0"
            }
        });
        var instructionId = Guid.NewGuid().ToString("N");
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