using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(AxonServerCollection))]
public class AxonServerCollection : ICollectionFixture<AxonServerContainer>
{
}