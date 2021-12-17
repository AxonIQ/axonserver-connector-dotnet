using System.Net;

namespace AxonIQ.AxonServer.Connector;

internal static class DnsEndPointExtensions
{
    public static Uri ToUri(this DnsEndPoint endpoint) =>
        new UriBuilder { Host = endpoint.Host, Port = endpoint.Port }.Uri;
}