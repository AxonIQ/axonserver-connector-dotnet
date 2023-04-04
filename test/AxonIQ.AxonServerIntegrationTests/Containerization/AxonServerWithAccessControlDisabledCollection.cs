using Xunit;

namespace AxonIQ.AxonServerIntegrationTests.Containerization;

[CollectionDefinition(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerWithAccessControlDisabledCollection : ICollectionFixture<AxonServerWithAccessControlDisabled>
{
}