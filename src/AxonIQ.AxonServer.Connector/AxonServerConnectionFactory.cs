using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    private readonly AxonServerGrpcChannelFactory _channelFactory;
    private readonly Scheduler _scheduler;
    private readonly ConcurrentDictionary<Context, Lazy<AxonServerConnection>> _connections;
    private readonly PermitCount _commandPermits;
    private readonly PermitCount _queryPermits;

    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ClientIdentity = new ClientIdentity(
            options.ComponentName, options.ClientInstanceId, options.ClientTags, new Version(1, 0));
        RoutingServers = options.RoutingServers;
        Authentication = options.Authentication;
        LoggerFactory = options.LoggerFactory;

        _scheduler = new Scheduler(
            options.Clock, 
            TimeSpan.FromMilliseconds(100),
            options.LoggerFactory.CreateLogger<Scheduler>());

        _channelFactory =
            new AxonServerGrpcChannelFactory(
                ClientIdentity, 
                Authentication, 
                RoutingServers, 
                options.LoggerFactory,
                options.GrpcChannelOptions);

        _commandPermits = options.CommandPermits;
        _queryPermits = options.QueryPermits;

        _connections = new ConcurrentDictionary<Context, Lazy<AxonServerConnection>>();
    }

    public ClientIdentity ClientIdentity { get; }
    public IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    public IAxonServerAuthentication Authentication { get; }
    public ILoggerFactory LoggerFactory { get; }

    public async Task<IAxonServerConnection> Connect(Context context)
    {
        var connection = _connections.GetOrAdd(context,
            _ => new Lazy<AxonServerConnection>(() => new AxonServerConnection(context, _channelFactory, _scheduler, _commandPermits, _queryPermits, LoggerFactory)))
            .Value;
        await connection.Connect();
        return connection;
    }
}