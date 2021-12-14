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

    public SystemProperties[] NodeProperties => Cluster.NodeProperties;

    public DnsEndPoint[] GetHttpEndpoints()
    {
        return Cluster.GetHttpEndpoints();
    }

    public HttpClient CreateHttpClient(int node)
    {
        return Cluster.CreateHttpClient(node);
    }

    public DnsEndPoint[] GetGrpcEndpoints()
    {
        return Cluster.GetGrpcEndpoints();
    }

    public GrpcChannel CreateGrpcChannel(int node, GrpcChannelOptions? options = default)
    {
        return Cluster.CreateGrpcChannel(node, options);
    }

    public Task DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}