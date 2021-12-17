using System.Net;
using Grpc.Net.Client;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonServer : IAsyncLifetime
{
    SystemProperties Properties { get; }
    
    DnsEndPoint GetHttpEndpoint();
    HttpClient CreateHttpClient();

    DnsEndPoint GetGrpcEndpoint();
    GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options);
}