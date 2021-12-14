using AxonIQ.AxonServer.Connector.Tests.Containerization;
using AxonIQ.AxonServer.Grpc.Control;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class CanAccessAxonServerContainer
{
    private readonly Containerization.AxonServer _container;
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
            //ComponentName = "Tests",
            Version = "1.2.3.4"
        });
        _logger.WriteLine(response.ToString());
    }
}