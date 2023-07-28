using PublicApiGenerator;

namespace AxonIQ.AxonServer.Connector.Tests;

[UsesVerify]
public class PublicApiAssertions
{
    [Fact]
    public Task VerifyApiChanges()
    {
        var publicApi = typeof(AxonServerConnectionFactory).Assembly.GeneratePublicApi();

        return Verify(publicApi);
    }
}