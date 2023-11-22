using System.Net;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactory : IAsyncDisposable
{
    private readonly AsyncLock _lock;
    private readonly Dictionary<Context, SharedAxonServerConnection> _connections;
    private long _disposed;

    public AxonServerConnectionFactory(AxonServerConnectorOptions options)
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
        CommandChannelInstructionTimeout = options.CommandChannelInstructionTimeout;
        CommandChannelInstructionPurgeFrequency = options.CommandChannelInstructionPurgeFrequency;
        QueryChannelInstructionTimeout = options.QueryChannelInstructionTimeout;
        QueryChannelInstructionPurgeFrequency = options.QueryChannelInstructionPurgeFrequency;
        ReconnectOptions = options.ReconnectOptions;

        _lock = new AsyncLock();
        _connections = new Dictionary<Context, SharedAxonServerConnection>();
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
    internal TimeSpan CommandChannelInstructionTimeout { get; }
    internal TimeSpan CommandChannelInstructionPurgeFrequency { get; }
    internal TimeSpan QueryChannelInstructionTimeout { get; }
    internal TimeSpan QueryChannelInstructionPurgeFrequency { get; }
    internal ReconnectOptions ReconnectOptions { get; }
    internal ILoggerFactory LoggerFactory { get; }

    public async Task<IAxonServerConnection> ConnectAsync(Context context, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();

        using (await _lock.AcquireAsync(ct))
        {
            if (_connections.TryGetValue(context, out var connection)) return connection;
            
            connection = new SharedAxonServerConnection(this, context);
            await connection.ConnectAsync().ConfigureAwait(false);
            _connections.Add(context, connection);
            return connection;
        }
    }

    internal async ValueTask TryRemoveConnectionAsync(Context context, SharedAxonServerConnection connection)
    {
        if (Interlocked.Read(ref _disposed) == Disposed.No)
        {
            using (await _lock.AcquireAsync(CancellationToken.None))
            {
                if (_connections.TryGetValue(context, out var removable))
                {
                    if (ReferenceEquals(removable, connection))
                    {
                        _connections.Remove(context);
                    }
                }
            }
        }
    }
    
    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes) 
            throw new ObjectDisposedException(nameof(AxonServerConnectionFactory));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            await _lock.DisposeAsync();
            await Scheduler.DisposeAsync().ConfigureAwait(false);

            foreach (var (_, connection) in _connections)
            {
                await connection.DisposeAsync();
            }
            _connections.Clear();
        }
    }
}