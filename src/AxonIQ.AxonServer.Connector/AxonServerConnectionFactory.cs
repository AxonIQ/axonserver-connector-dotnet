using System.Collections.Concurrent;
using System.Net;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory
{
    private readonly ConcurrentDictionary<Context, Lazy<AxonServerConnection>> _connections;

    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ClientIdentity = new ClientIdentity(
            options.ComponentName, options.ClientInstanceId, options.ClientTags, new Version(1, 0));
        RoutingServers = options.RoutingServers;
        Authentication = options.Authentication;
        LoggerFactory = options.LoggerFactory;

        Scheduler = new Scheduler(
            options.Clock, 
            TimeSpan.FromMilliseconds(100),
            options.LoggerFactory.CreateLogger<Scheduler>());

        ChannelFactory =
            new AxonServerGrpcChannelFactory(
                ClientIdentity, 
                options.Authentication, 
                options.RoutingServers, 
                options.LoggerFactory, 
                options.Interceptors,
                (options.GrpcChannelOptions?.Clone() ?? new GrpcChannelOptions()).ConfigureAxonOptions());

        Interceptors = options.Interceptors;
        CommandPermits = options.CommandPermits;
        QueryPermits = options.QueryPermits;
        EventProcessorUpdateFrequency = options.EventProcessorUpdateFrequency;
        ConnectBackoffPolicyOptions = options.ConnectBackoffPolicyOptions;

        _connections = new ConcurrentDictionary<Context, Lazy<AxonServerConnection>>();
    }

    public ClientIdentity ClientIdentity { get; }
    public IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    public IAxonServerAuthentication Authentication { get; }
    public Scheduler Scheduler { get; }
    public AxonServerGrpcChannelFactory ChannelFactory { get; }
    public IReadOnlyList<Interceptor> Interceptors { get; }
    public PermitCount CommandPermits { get; }
    public PermitCount QueryPermits { get; }
    public TimeSpan EventProcessorUpdateFrequency { get; }
    public BackoffPolicyOptions ConnectBackoffPolicyOptions { get; }
    public ILoggerFactory LoggerFactory { get; }

    public async Task<IAxonServerConnection> Connect(Context context)
    {
        var connection = _connections.GetOrAdd(context,
            _ => new Lazy<AxonServerConnection>(() => new AxonServerConnection(context, ChannelFactory, Interceptors, Scheduler, CommandPermits, QueryPermits, EventProcessorUpdateFrequency, ConnectBackoffPolicyOptions, LoggerFactory)))
            .Value;
        await connection.Connect().ConfigureAwait(false);
        return connection;
    }
}