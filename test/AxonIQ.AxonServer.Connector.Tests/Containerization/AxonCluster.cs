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

    public IReadOnlyList<DnsEndPoint> GetGrpcEndpoints()
    {
        return Cluster.GetGrpcEndpoints();
    }

    public Task DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}