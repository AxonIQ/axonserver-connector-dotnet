using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonClusterNode
{
    SystemProperties Properties { get; }

    DnsEndPoint GetHttpEndpoint();
    HttpClient CreateHttpClient();

    DnsEndPoint GetGrpcEndpoint();
    GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options);
}