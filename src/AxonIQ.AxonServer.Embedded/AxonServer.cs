using System.Net;
using Grpc.Net.Client;

namespace AxonIQ.AxonServer.Embedded;

public abstract class AxonServer : IAxonServer
{
    protected abstract IAxonServer Server { get; }

    public Task InitializeAsync()
    {
        return Server.InitializeAsync();
    }
    
    public Task WaitUntilAvailableAsync(TimeSpan? maximumWaitTime = default, TimeSpan? delayBetweenAttempts = default)
    {
        return Server.WaitUntilAvailableAsync(maximumWaitTime, delayBetweenAttempts);
    }

    public SystemProperties Properties => Server.Properties;

    public DnsEndPoint GetHttpEndpoint()
    {
        return Server.GetHttpEndpoint();
    }

    public HttpClient CreateHttpClient()
    {
        return Server.CreateHttpClient();
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return Server.GetGrpcEndpoint();
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options = default)
    {
        return Server.CreateGrpcChannel(options);
    }

    public async Task PurgeEvents()
    {
        using var client = Server.CreateHttpClient();
        (await client.DeleteAsync("v1/devmode/purge-events")).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        return Server.DisposeAsync();
    }
}