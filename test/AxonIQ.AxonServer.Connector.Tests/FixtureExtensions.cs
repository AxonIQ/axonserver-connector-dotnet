using AutoFixture;

namespace AxonIQ.AxonServer.Connector.Tests;

public static class FixtureExtensions
{
    public static void CustomizeComponentName(this IFixture fixture)
    {
        fixture.Customize<ComponentName>(composer =>
            composer
                .FromFactory(ComponentName.GenerateRandomName)
                .OmitAutoProperties());
    }

    public static void CustomizeClientId(this IFixture fixture)
    {
        fixture.Customize<ClientId>(composer =>
            composer
                .FromFactory((ComponentName name) => ClientId.GenerateFrom(name))
                .OmitAutoProperties());
    }
}