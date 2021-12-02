using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonServerContainerWithAccessControlEnabledCollection))]
public class
    AxonServerContainerWithAccessControlEnabledCollection : ICollectionFixture<
        AxonServerContainerWithAccessControlEnabled>
{
}