using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class AxonServerContainer : IAxonServerContainer
{
    protected abstract IAxonServerContainer Container { get; }

    public Task InitializeAsync()
    {
        return Container.InitializeAsync();
    }

    public DnsEndPoint GetHttpEndpoint()
    {
        return Container.GetHttpEndpoint();
    }

    public HttpClient CreateHttpClient()
    {
        return Container.CreateHttpClient();
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return Container.GetGrpcEndpoint();
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options = default)
    {
        return Container.CreateGrpcChannel(options);
    }

    public async Task PurgeEvents()
    {
        using var client = Container.CreateHttpClient();
        (await client.DeleteAsync("v1/devmode/purge-events")).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        return Container.DisposeAsync();
    }
}