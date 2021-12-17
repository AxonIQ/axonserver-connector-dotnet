using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlEnabledCollection))]
public class AxonServerWithAccessControlEnabledCollection : ICollectionFixture<AxonServerWithAccessControlEnabled>
{
}