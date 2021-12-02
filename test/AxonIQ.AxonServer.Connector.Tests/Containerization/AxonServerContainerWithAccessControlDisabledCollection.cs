using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonServerContainerWithAccessControlDisabledCollection))]
public class
    AxonServerContainerWithAccessControlDisabledCollection : ICollectionFixture<
        AxonServerContainerWithAccessControlDisabled>
{
}