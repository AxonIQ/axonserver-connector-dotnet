using Xunit;

namespace AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerWithAccessControlDisabledCollection : ICollectionFixture<AxonServerWithAccessControlDisabled>
{
}