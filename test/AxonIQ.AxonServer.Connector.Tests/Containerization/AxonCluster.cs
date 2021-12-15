using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class AxonCluster : IAxonCluster
{
    protected abstract IAxonCluster Cluster { get; }

    public Task InitializeAsync()
    {
        return Cluster.InitializeAsync();
    }

    public IAxonClusterNode[] Nodes => Cluster.Nodes;

    public Context[] Contexts => Cluster.Contexts;

    public DnsEndPoint[] GetHttpEndpoints()
    {
        return Cluster.GetHttpEndpoints();
    }

    public DnsEndPoint[] GetGrpcEndpoints()
    {
        return Cluster.GetGrpcEndpoints();
    }

    public Task DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}