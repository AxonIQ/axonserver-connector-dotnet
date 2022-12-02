using System.Net;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class AxonCluster : IAxonCluster
{
    protected abstract IAxonCluster Cluster { get; }

    public Task InitializeAsync()
    {
        return Cluster.InitializeAsync();
    }

    public IReadOnlyList<IAxonClusterNode> Nodes => Cluster.Nodes;

    public IReadOnlyList<Context> Contexts => Cluster.Contexts;

    public IReadOnlyList<DnsEndPoint> GetHttpEndpoints()
    {
        return Cluster.GetHttpEndpoints();
    }
    
    public DnsEndPoint GetRandomHttpEndpoint()
    {
        var endpoints = Cluster.GetHttpEndpoints();
        return endpoints[Random.Shared.Next(0, endpoints.Count)];
    }

    public IReadOnlyList<DnsEndPoint> GetGrpcEndpoints()
    {
        return Cluster.GetGrpcEndpoints();
    }
    
    public DnsEndPoint GetRandomGrpcEndpoint()
    {
        var endpoints = Cluster.GetGrpcEndpoints();
        return endpoints[Random.Shared.Next(0, endpoints.Count)];
    }

    public Task DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}