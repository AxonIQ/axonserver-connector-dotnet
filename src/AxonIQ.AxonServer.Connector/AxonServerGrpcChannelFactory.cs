using System.Net;
using AxonIQ.AxonServer.Grpc.Control;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerGrpcChannelFactory
{
    private readonly ClientIdentity _clientIdentity;
    private readonly IAxonServerAuthentication _authentication;
    private readonly IReadOnlyList<DnsEndPoint> _routingServers;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AxonServerGrpcChannelFactory> _logger;

    public AxonServerGrpcChannelFactory(
        ClientIdentity clientIdentity,
        IAxonServerAuthentication authentication,
        IReadOnlyList<DnsEndPoint> routingServers,
        ILoggerFactory loggerFactory)
    {
        _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _routingServers = routingServers ?? throw new ArgumentNullException(nameof(routingServers));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>();
    }

    public async Task<GrpcChannel?> Create(Context context)
    {
        GrpcChannel? channel = null;
        var index = 0;
        while (channel == null && index < _routingServers.Count)
        {
            var server = _routingServers[index];
            _logger.LogInformation("Requesting connection details from {Host}:{Port}", server.Host, server.Port);
            var candidate = GrpcChannel.ForAddress(server.ToUri(), new GrpcChannelOptions
            {
                LoggerFactory = _loggerFactory
            });
            var callInvoker = candidate.Intercept(metadata =>
            {
                _authentication.WriteTo(metadata);
                context.WriteTo(metadata);
                return metadata;
            });
            try
            {
                var service = new PlatformService.PlatformServiceClient(callInvoker);
                var info = await service.GetPlatformServerAsync(
                    _clientIdentity.ToClientIdentification()
                    //, new CallOptions(deadline: DateTime.Now)
                );
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
                    await candidate.ShutdownAsync();
                    _logger.LogInformation("Connecting to [{NodeName}] ({Host}:{Port})",
                        info.Primary.NodeName,
                        info.Primary.HostName,
                        info.Primary.GrpcPort);
                    var primaryServer = new DnsEndPoint(info.Primary.HostName, info.Primary.GrpcPort);
                    channel = GrpcChannel.ForAddress(primaryServer.ToUri(), new GrpcChannelOptions
                    {
                        LoggerFactory = _loggerFactory
                    });
                }
            }
            catch (Exception exception)
            {
                await candidate.ShutdownAsync();
                _logger.LogWarning(exception, "Connecting to AxonServer node [{Host}:{Port}] failed", server.Host,
                    server.Port);
            }

            index++;
        }

        return channel;
    }
}