using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Embedded;

public interface IAxonServer
{
    SystemProperties Properties { get; }
    
    DnsEndPoint GetHttpEndpoint();
    HttpClient CreateHttpClient();

    DnsEndPoint GetGrpcEndpoint();
    GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options);
    
    Task InitializeAsync();
    Task DisposeAsync();
}