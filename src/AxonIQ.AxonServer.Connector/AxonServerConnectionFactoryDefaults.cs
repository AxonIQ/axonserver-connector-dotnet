using System.Net;

namespace AxonIQ.AxonServer.Connector;

public static class AxonServerConnectionFactoryDefaults
{
    public static readonly int Port = 8124;
    
    public static readonly IReadOnlyCollection<DnsEndPoint> RoutingServers = new[]
        { new DnsEndPoint("localhost", Port) };

    public static readonly IReadOnlyDictionary<string, string> ClientTags = new Dictionary<string, string>();

    public static readonly IAxonServerAuthentication Authentication = AxonServerAuthentication.None;

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(10_000);

    public static readonly PermitCount MinimumCommandPermits = new(16);
    public static readonly PermitCount MinimumQueryPermits = new(16);
    public static readonly PermitCount DefaultQueryPermits = new(5000);
    public static readonly PermitCount DefaultCommandPermits = new(5000);
}