using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

[CollectionDefinition(nameof(ToxicAxonServerWithAccessControlDisabledCollection))]
public class ToxicAxonServerWithAccessControlDisabledCollection : ICollectionFixture<ToxicAxonServerWithAccessControlDisabled>
{
}