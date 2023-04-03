using Xunit;

namespace AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlEnabledCollection))]
public class AxonServerWithAccessControlEnabledCollection : ICollectionFixture<AxonServerWithAccessControlEnabled>
{
}