using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Embedded;

public interface IAxonClusterNode
{
    SystemProperties Properties { get; }
    ClusterTemplate Template { get; }

    DnsEndPoint GetHttpEndpoint();
    HttpClient CreateHttpClient();

    DnsEndPoint GetGrpcEndpoint();
    GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options);
}