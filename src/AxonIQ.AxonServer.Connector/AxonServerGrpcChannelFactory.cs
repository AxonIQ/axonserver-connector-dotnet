using System.Net;
using Io.Axoniq.Axonserver.Grpc.Control;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerGrpcChannelFactory
{
    private readonly ILogger<AxonServerGrpcChannelFactory> _logger;

    public AxonServerGrpcChannelFactory(
        ClientIdentity clientIdentity,
        IAxonServerAuthentication authentication,
        IReadOnlyList<DnsEndPoint> routingServers,
        ILoggerFactory loggerFactory,
        IReadOnlyList<Interceptor> interceptors,
        GrpcChannelOptions grpcChannelOptions)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        RoutingServers = routingServers ?? throw new ArgumentNullException(nameof(routingServers));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        Interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
        GrpcChannelOptions = grpcChannelOptions ?? throw new ArgumentNullException(nameof(grpcChannelOptions));
        _logger = loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>();
    }
    
    public ClientIdentity ClientIdentity { get; }
    public IAxonServerAuthentication Authentication { get; }
    public IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    public ILoggerFactory LoggerFactory { get; }
    public IReadOnlyList<Interceptor> Interceptors { get; }
    public GrpcChannelOptions GrpcChannelOptions { get; }

    public async Task<GrpcChannel?> Create(Context context)
    {
        GrpcChannel? channel = null;
        var index = 0;
        while (channel == null && index < RoutingServers.Count)
        {
            
            var server = RoutingServers[index];
            _logger.LogInformation("Requesting connection details from {Host}:{Port}", server.Host, server.Port);
            var candidate = GrpcChannel.ForAddress(server.ToUri(), GrpcChannelOptions);
            var callInvoker = candidate
                .Intercept(Interceptors.ToArray())
                .Intercept(metadata =>
                {
                    Authentication.WriteTo(metadata);
                    context.WriteTo(metadata);
                    return metadata;
                });
            try
            {
                var service = new PlatformService.PlatformServiceClient(callInvoker);
                var info = await service.GetPlatformServerAsync(
                    ClientIdentity.ToClientIdentification()
                    //, new CallOptions(deadline: DateTime.Now)
                ).ConfigureAwait(false);
                _logger.LogDebug("Received PlatformInfo suggesting [{NodeName}] ({Host}:{Port}), {SameConnection}",
                    info.Primary.NodeName,
                    info.Primary.HostName,
                    info.Primary.GrpcPort,
                    info.SameConnection
                        ? "allowing use of existing connection"
                        : "requiring new connection");
                if (info.SameConnection || info.Primary.HostName == server.Host && info.Primary.GrpcPort == server.Port)
                {
                    _logger.LogDebug("Reusing existing channel");
                    channel = candidate;
                }
                else
                {
                    await candidate.ShutdownAsync().ConfigureAwait(false);
                    _logger.LogInformation("Connecting to [{NodeName}] ({Host}:{Port})",
                        info.Primary.NodeName,
                        info.Primary.HostName,
                        info.Primary.GrpcPort);
                    var primaryServer = new DnsEndPoint(info.Primary.HostName, info.Primary.GrpcPort);
                    channel = GrpcChannel.ForAddress(primaryServer.ToUri(), GrpcChannelOptions);
                }
            }
            catch (Exception exception)
            {
                await candidate.ShutdownAsync().ConfigureAwait(false);
                _logger.LogWarning(exception, "Connecting to AxonServer node [{Host}:{Port}] failed", server.Host,
                    server.Port);
            }

            index++;
        }

        return channel;
    }
}