using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Io.Axoniq.Axonserver.Grpc.Control;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class CanAccessAxonServerContainer
{
    private readonly Embedded.AxonServer _container;
    private readonly ITestOutputHelper _logger;

    public CanAccessAxonServerContainer(AxonServerWithAccessControlDisabled container,
        ITestOutputHelper logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Fact]
    public async Task Proof()
    {
        // This is where we should be able to talk to the axon server using our client
        await _container.PurgeEvents();

        using var channel = _container.CreateGrpcChannel();
        var service = new PlatformService.PlatformServiceClient(channel);
        var response = await service.GetPlatformServerAsync(new ClientIdentification
        {
            ClientId = Guid.NewGuid().ToString("N"),
            ComponentName = "Tests",
            Version = "1.2.3.4"
        });
        _logger.WriteLine(response.ToString());

        // var stream = service.OpenStream();
        // await stream.RequestStream.WriteAsync(new PlatformInboundInstruction
        // {
        //     Register = new ClientIdentification
        //     {
        //         ClientId = Guid.NewGuid().ToString("N"),
        //         ComponentName = "Tests",
        //         Version = "1.2.3.4"
        //     }
        // });
        // await foreach (var outboundInstruction in stream.ResponseStream.ReadAllAsync())
        // {
        //     _logger.WriteLine(outboundInstruction.RequestCase.ToString());
        //     switch (outboundInstruction.RequestCase)
        //     {
        //         case PlatformOutboundInstruction.RequestOneofCase.None:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.NodeNotification:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.RequestReconnect:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.PauseEventProcessor:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.StartEventProcessor:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.ReleaseSegment:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.RequestEventProcessorInfo:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.SplitEventProcessorSegment:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.MergeEventProcessorSegment:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.Heartbeat:
        //             break;
        //         case PlatformOutboundInstruction.RequestOneofCase.Ack:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException();
        //     }
        // }
    }
}