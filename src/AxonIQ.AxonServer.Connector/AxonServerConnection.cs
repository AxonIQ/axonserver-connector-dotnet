using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnection
{
    private readonly ClientIdentity _clientIdentity;
    private readonly IReadOnlyCollection<DnsEndPoint> _routingServers;
    private readonly Context _context;

    public AxonServerConnection(
        ClientIdentity clientIdentity,
        IReadOnlyCollection<DnsEndPoint> routingServers,
        Context context)
    {
        _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        _routingServers = routingServers;
        _context = context;
    }

    public Task Connect()
    {
        GrpcChannel? channel = null;
        foreach (var server in _routingServers)
        {
            channel = GrpcChannel.ForAddress(server.ToUri());
        }

        return Task.CompletedTask;
    }
}