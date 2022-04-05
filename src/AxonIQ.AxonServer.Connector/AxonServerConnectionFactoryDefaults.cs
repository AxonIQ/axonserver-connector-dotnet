using System.Net;

namespace AxonIQ.AxonServer.Connector;

public static class AxonServerConnectionFactoryDefaults
{
    public static readonly IReadOnlyCollection<DnsEndPoint> RoutingServers = new[]
        { new DnsEndPoint("localhost", 8124) };

    public static readonly IReadOnlyDictionary<string, string> ClientTags = new Dictionary<string, string>();

    public static readonly IAxonServerAuthentication Authentication = AxonServerAuthentication.None;

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(10_000);

    public static readonly PermitCount MinimumCommandPermits = new PermitCount(16);
    public static readonly PermitCount MinimumQueryPermits = new PermitCount(16);
    public static readonly PermitCount DefaultQueryPermits = new PermitCount(5000);
    public static readonly PermitCount DefaultCommandPermits = new PermitCount(5000);
}