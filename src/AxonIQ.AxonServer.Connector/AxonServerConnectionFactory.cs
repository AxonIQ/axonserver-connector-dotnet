using System.Collections.Concurrent;
using System.Net;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    private readonly ConcurrentDictionary<Context, AxonServerConnection> _connections;

    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ClientIdentity = new ClientIdentity(
            options.ComponentName, options.ClientInstanceId, options.ClientTags, new Version(1, 0));
        RoutingServers = options.RoutingServers;
        Authentication = options.Authentication;

        _connections = new ConcurrentDictionary<Context, AxonServerConnection>();
    }

    public ClientIdentity ClientIdentity { get; }
    public IReadOnlyCollection<DnsEndPoint> RoutingServers { get; }
    public IAxonServerAuthentication Authentication { get; }

    public Task<AxonServerConnection> Connect(Context context)
    {
        return Task.FromResult(_connections.GetOrAdd(context,
            _ => new AxonServerConnection(
                ClientIdentity,
                RoutingServers,
                context)));
    }
}