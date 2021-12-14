using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonClusterWithAccessControlDisabledCollection))]
public class AxonClusterWithAccessControlDisabledCollection : ICollectionFixture<AxonClusterWithAccessControlDisabled>
{
}