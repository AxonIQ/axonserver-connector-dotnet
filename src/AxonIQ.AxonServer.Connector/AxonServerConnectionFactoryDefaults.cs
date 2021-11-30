using System.Net;

namespace AxonIQ.AxonServer.Connector;

public static class AxonServerConnectionFactoryDefaults
{
    public static readonly ComponentName ComponentName = new("Unnamed");

    public static readonly IReadOnlyCollection<DnsEndPoint> RoutingServers = new[]
        { new DnsEndPoint("localhost", 8124) };

    public static readonly IReadOnlyDictionary<string, string> ClientTags = new Dictionary<string, string>();

    public static readonly IAxonServerAuthentication Authentication = AxonServerAuthentication.None;
}