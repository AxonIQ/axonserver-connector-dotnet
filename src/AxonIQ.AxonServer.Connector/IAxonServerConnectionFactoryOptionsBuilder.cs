using System.Net;

namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnectionFactoryOptionsBuilder
{
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(params DnsEndPoint[] servers);
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(IEnumerable<DnsEndPoint> servers);
    IAxonServerConnectionFactoryOptionsBuilder WithoutAuthentication();
    IAxonServerConnectionFactoryOptionsBuilder WithAuthenticationToken(string token);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTag(string key, string value);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(params KeyValuePair<string, string>[] tags);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(IEnumerable<KeyValuePair<string, string>> tags);

    AxonServerConnectionFactoryOptions Build();
}