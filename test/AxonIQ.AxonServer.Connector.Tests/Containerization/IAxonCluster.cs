using System.Net;
using Grpc.Net.Client;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonCluster : IAsyncLifetime
{
    SystemProperties[] NodeProperties { get; }

    DnsEndPoint[] GetHttpEndpoints();
    HttpClient CreateHttpClient(int node);

    DnsEndPoint[] GetGrpcEndpoints();
    GrpcChannel CreateGrpcChannel(int node, GrpcChannelOptions? options);
}