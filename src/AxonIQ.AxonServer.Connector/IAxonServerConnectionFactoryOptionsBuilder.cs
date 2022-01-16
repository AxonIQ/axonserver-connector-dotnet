using System.Net;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnectionFactoryOptionsBuilder
{
    IAxonServerConnectionFactoryOptionsBuilder AsComponentName(ComponentName name);
    IAxonServerConnectionFactoryOptionsBuilder AsClientInstanceId(ClientInstanceId id);
    IAxonServerConnectionFactoryOptionsBuilder WithDefaultRoutingServers();
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(params DnsEndPoint[] servers);
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(IEnumerable<DnsEndPoint> servers);
    IAxonServerConnectionFactoryOptionsBuilder WithoutAuthentication();
    IAxonServerConnectionFactoryOptionsBuilder WithAuthenticationToken(string token);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTag(string key, string value);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(params KeyValuePair<string, string>[] tags);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(IEnumerable<KeyValuePair<string, string>> tags);
    IAxonServerConnectionFactoryOptionsBuilder WithLoggerFactory(ILoggerFactory loggerFactory);
    IAxonServerConnectionFactoryOptionsBuilder WithClock(Func<DateTimeOffset> clock);
    IAxonServerConnectionFactoryOptionsBuilder WithGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions);

    AxonServerConnectionFactoryOptions Build();
}