using Xunit;

namespace AxonIQ.AxonServerIntegrationTests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlEnabledCollection))]
public class AxonServerWithAccessControlEnabledCollection : ICollectionFixture<AxonServerWithAccessControlEnabled>
{
}