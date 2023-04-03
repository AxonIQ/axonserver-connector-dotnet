using Xunit;

namespace AxonIQ.AxonClusterIntegrationTests.Containerization;

[CollectionDefinition(nameof(AxonClusterWithAccessControlEnabledCollection))]
public class AxonClusterWithAccessControlEnabledCollection : ICollectionFixture<AxonClusterWithAccessControlEnabled>
{
}