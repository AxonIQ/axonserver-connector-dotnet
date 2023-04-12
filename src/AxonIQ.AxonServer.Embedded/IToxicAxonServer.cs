using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Embedded;

public interface IToxicAxonServer : IAxonServer
{
    DnsEndPoint GetGrpcProxyEndpoint();
    GrpcChannel CreateGrpcProxyChannel(GrpcChannelOptions? options);

    Task DisableGrpcProxyEndpointAsync();
    Task EnableGrpcProxyEndpointAsync();
    Task<IAsyncDisposable> ResetPeerOnGrpcProxyEndpointAsync(int? timeout = default);
    Task<IAsyncDisposable> TimeoutEndpointAsync(int? timeout = default);
}