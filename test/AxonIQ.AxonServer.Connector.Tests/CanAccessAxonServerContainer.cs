using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerCollection))]
public class CanAccessAxonServerContainer
{
    private readonly AxonServerContainer _container;

    public CanAccessAxonServerContainer(AxonServerContainer container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    [Fact]
    public async Task Proof()
    {
        // This is where we should be able to talk to the axon server using our client
        await _container.PurgeEvents();
    }
}