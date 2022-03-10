using System.Net;
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

    public static void CustomizeContext(this IFixture fixture)
    {
        fixture.Customize<Context>(composer =>
            composer
                .FromFactory((string value) => new Context(value))
                .OmitAutoProperties());
    }

    public static void CustomizeClientInstanceId(this IFixture fixture)
    {
        fixture.Customize<ClientInstanceId>(composer =>
            composer
                .FromFactory((ComponentName name) => ClientInstanceId.GenerateFrom(name))
                .OmitAutoProperties());
    }
    
    public static void CustomizeLoadFactor(this IFixture fixture)
    {
        fixture.Customize<LoadFactor>(composer =>
            composer
                .FromFactory((int value) => new LoadFactor(Math.Abs(value)))
                .OmitAutoProperties());
    }

    public static void CustomizeLocalHostDnsEndPointInReservedPortRange(this IFixture fixture)
    {
        // REMARK: Due to the randomization of data we might accidentally pick a port on which an AxonServer is listening.
        // Since no Axon Server will be listening on a port in the reserved port range [0..1024], we prevent this
        // accident from happening.
        fixture.Customize<DnsEndPoint>(composer =>
            composer
                .FromFactory((int port) => new DnsEndPoint("localhost", port % 1024))
                .OmitAutoProperties());
    }
}