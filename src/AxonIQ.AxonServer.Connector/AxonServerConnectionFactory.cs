using System.Collections.Concurrent;
using System.Net;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Context, Lazy<AxonServerConnection>> _connections;
    private bool _disposed;

    public AxonServerConnectionFactory(AxonServerConnectionFactoryOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        ClientIdentity = new ClientIdentity(
            options.ComponentName, 
            options.ClientInstanceId, 
            options.ClientTags, 
            new Version(1, 0));
        
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
                options.GrpcChannelOptions ?? new GrpcChannelOptions(),
                options.Clock,
                options.ReconnectOptions.ConnectionTimeout);

        Interceptors = options.Interceptors;
        CommandPermits = options.CommandPermits;
        QueryPermits = options.QueryPermits;
        EventProcessorUpdateFrequency = options.EventProcessorUpdateFrequency;
        ReconnectOptions = options.ReconnectOptions;

        _connections = new ConcurrentDictionary<Context, Lazy<AxonServerConnection>>();
    }

    internal ClientIdentity ClientIdentity { get; }
    internal IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    internal IAxonServerAuthentication Authentication { get; }
    internal Scheduler Scheduler { get; }
    internal AxonServerGrpcChannelFactory ChannelFactory { get; }
    internal IReadOnlyList<Interceptor> Interceptors { get; }
    internal PermitCount CommandPermits { get; }
    internal PermitCount QueryPermits { get; }
    internal TimeSpan EventProcessorUpdateFrequency { get; }
    internal ReconnectOptions ReconnectOptions { get; }
    internal ILoggerFactory LoggerFactory { get; }

    public async Task<IAxonServerConnection> ConnectAsync(Context context)
    {
        ThrowIfDisposed();
        
        var connection = _connections.GetOrAdd(context,
            _ => new Lazy<AxonServerConnection>(() => new AxonServerConnection(this, context)))
            .Value;
        await connection.ConnectAsync().ConfigureAwait(false);
        return connection;
    }

    internal void TryRemoveConnection(Context context, AxonServerConnection connection)
    {
        //NOTE: Only if the value of the Lazy<T> instance matches the connection we're about to remove, do we attempt
        //      to remove the corresponding lazy instance from the collection.
        if (!_connections.TryGetValue(context, out var lazy)) return;

        if (lazy.IsValueCreated && lazy.Value == connection)
        {
            _connections.TryRemove(new KeyValuePair<Context, Lazy<AxonServerConnection>>(context, lazy));
        }
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AxonServerConnection));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await Scheduler.DisposeAsync().ConfigureAwait(false);

        var connections = Array.ConvertAll(_connections.ToArray(), connection => connection.Value);
        _connections.Clear();
        
        foreach (var connection in connections)
        {
            if (connection.IsValueCreated)
            {
                await connection.Value.DisposeAsync().ConfigureAwait(false);
            }
        }
        
        _disposed = true;
    }
}