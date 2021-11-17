using System.Net.Http.Headers;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests;

/// <summary>
/// Manages the interaction with an embedded container.
/// </summary>
public class EmbeddedAxonServerContainer : IAxonServerContainer
{
    private readonly IMessageSink _logger;
    private IContainerService? _container;

    public EmbeddedAxonServerContainer(IMessageSink logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public async Task InitializeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container is being initialized"));
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
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container got started"));
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
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container became available"));
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container is initialized"));
    }

    public Task DisposeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container is being disposed"));
        if (_container != null)
        {
            _container.Remove(true);
            _container.Dispose();
        }
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container got disposed"));
        return Task.CompletedTask;
    }
}
