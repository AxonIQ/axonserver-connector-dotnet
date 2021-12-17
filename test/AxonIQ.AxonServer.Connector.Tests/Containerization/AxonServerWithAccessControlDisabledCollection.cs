using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerWithAccessControlDisabledCollection : ICollectionFixture<AxonServerWithAccessControlDisabled>
{
}