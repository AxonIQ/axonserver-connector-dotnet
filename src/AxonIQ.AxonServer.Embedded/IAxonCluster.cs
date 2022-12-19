using System.Net;
using AxonIQ.AxonServer.Connector;

namespace AxonIQ.AxonServer.Embedded;

public interface IAxonCluster
{
    IReadOnlyList<IAxonClusterNode> Nodes { get; }
    
    IReadOnlyList<Context> Contexts { get; }

    IReadOnlyList<DnsEndPoint> GetHttpEndpoints();
    
    DnsEndPoint GetRandomHttpEndpoint();

    IReadOnlyList<DnsEndPoint> GetGrpcEndpoints();
    
    DnsEndPoint GetRandomGrpcEndpoint();
    
    Task InitializeAsync();
    Task DisposeAsync();
}