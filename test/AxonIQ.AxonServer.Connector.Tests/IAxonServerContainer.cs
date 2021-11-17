using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public interface IAxonServerContainer : IAsyncLifetime
{
    HttpClient CreateClient();
}