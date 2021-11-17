using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[CollectionDefinition(nameof(AxonServerCollection))]
public class AxonServerCollection : ICollectionFixture<AxonServerContainer>
{
}