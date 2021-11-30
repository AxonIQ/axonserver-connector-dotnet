using System.Net;
using Grpc.Net.Client;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public interface IAxonServerContainer : IAsyncLifetime
{
    DnsEndPoint GetHttpEndpoint();
    HttpClient CreateHttpClient();

    DnsEndPoint GetGrpcEndpoint();
    GrpcChannel CreateGrpcChannel();
}