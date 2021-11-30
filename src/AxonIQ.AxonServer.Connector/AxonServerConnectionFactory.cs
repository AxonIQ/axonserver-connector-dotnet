using System.Collections.Concurrent;
using System.Net;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    private readonly ConcurrentDictionary<string, AxonServerConnection> _connections;

    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ComponentName = options.ComponentName;
        ClientInstanceId = options.ClientInstanceId;
        RoutingServers = options.RoutingServers;
        ClientTags = options.ClientTags;
        Authentication = options.Authentication;

        _connections = new ConcurrentDictionary<string, AxonServerConnection>();
    }

    public ComponentName ComponentName { get; }
    public ClientId ClientInstanceId { get; }
    public IReadOnlyCollection<DnsEndPoint> RoutingServers { get; }
    public IReadOnlyDictionary<string, string> ClientTags { get; }
    public IAxonServerAuthentication Authentication { get; }

    public Task<AxonServerConnection> Connect(string context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        return Task.FromResult(_connections.GetOrAdd(context, _ => new AxonServerConnection()));
    }
}