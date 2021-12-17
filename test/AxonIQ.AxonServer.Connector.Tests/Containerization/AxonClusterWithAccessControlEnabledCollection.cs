using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonClusterWithAccessControlEnabledCollection))]
public class AxonClusterWithAccessControlEnabledCollection : ICollectionFixture<AxonClusterWithAccessControlEnabled>
{
}