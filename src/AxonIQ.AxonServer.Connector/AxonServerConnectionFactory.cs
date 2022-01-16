using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    private readonly AxonServerGrpcChannelFactory _channelFactory;
    private readonly Scheduler _scheduler;
    private readonly ConcurrentDictionary<Context, AxonServerConnection> _connections;

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
        _connections = new ConcurrentDictionary<Context, AxonServerConnection>();
    }

    public ClientIdentity ClientIdentity { get; }
    public IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    public IAxonServerAuthentication Authentication { get; }
    public ILoggerFactory LoggerFactory { get; }

    public async Task<IAxonServerConnection> Connect(Context context)
    {
        //Note: The valueFactory is not thread safe, but in the odd case of a race,
        //the instances that lost the race will be garbage collected anyway.
        //Such an instance does not hold any precious resources until a connection is established.
        var connection = _connections.GetOrAdd(context,
            _ => new AxonServerConnection(context, _channelFactory, _scheduler, LoggerFactory));
        await connection.Connect();
        return connection;
    }
}