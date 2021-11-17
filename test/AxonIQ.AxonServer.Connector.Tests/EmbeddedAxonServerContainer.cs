using System.Net.Http.Headers;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;

namespace AxonIQ.AxonServer.Connector.Tests;

/// <summary>
/// Manages the interaction with an embedded container.
/// </summary>
public class EmbeddedAxonServerContainer : IAxonServerContainer
{
    private IContainerService? _container;
    
    public async Task InitializeAsync()
    {
        _container = new Builder()
            .UseContainer()
            .UseImage("axoniq/axonserver")
            .ExposePort(8024)
            .ExposePort(8124)
            .WithEnvironment(
                "AXONIQ_AXONSERVER_NAME=axonserver",
                "AXONIQ_AXONSERVER_HOSTNAME=localhost",
                "AXONIQ_AXONSERVER_DEVMODE_ENABLED=true")
            .WaitForPort("8024/tcp", TimeSpan.FromSeconds(10.0))
            .Build()
            .Start();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var endpoint = _container.ToHostExposedEndpoint("8024/tcp");
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = endpoint.Port,
            Path = "actuator/health"
        }.Uri;

        var available = false;
        while (!available)
        {
            try
            {
                (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                available = true;
            }
            catch(HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }

    public Task DisposeAsync()
    {
        if (_container != null)
        {
            _container.Remove(true);
            _container.Dispose();
        }
        return Task.CompletedTask;
    }
}
