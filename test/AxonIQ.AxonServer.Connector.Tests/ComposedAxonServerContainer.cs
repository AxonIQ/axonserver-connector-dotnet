using System.Net.Http.Headers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests;

/// <summary>
/// Manages the interaction with a container composed in the CI environment.
/// </summary>
public class ComposedAxonServerContainer : IAxonServerContainer
{
    private readonly IMessageSink _logger;

    public ComposedAxonServerContainer(IMessageSink logger)
    {
        if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_PORT") == null)
        {
            throw new InvalidOperationException("The AXONIQ_AXONSERVER_PORT environment variable is missing.");
        }

        if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_GRPC_PORT") == null)
        {
            throw new InvalidOperationException("The AXONIQ_AXONSERVER_GRPC_PORT environment variable is missing.");
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is being initialized"));
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_PORT")!),
            Path = "actuator/health"
        }.Uri;

        var available = false;
        const int maximumAttempts = 60;
        var attempt = 0;
        while (!available && attempt < maximumAttempts)
        {
            _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is being health checked at {0}",
                requestUri.AbsoluteUri));
            try
            {
                (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                available = true;
            }
            catch (HttpRequestException exception)
            {
                _logger.OnMessage(new DiagnosticMessage(
                    "Composed Axon Server Container could not be reached at {0} because {1}",
                    requestUri.AbsoluteUri,
                    exception));
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Composed Axon Server Container could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} after {maximumAttempts} attempts");
        }

        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container became available"));
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is initialized"));
    }

    public HttpClient CreateClient()
    {
        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = "localhost",
                Port = int.Parse(Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_PORT")!)
            }.Uri
        };
    }

    public Task DisposeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is being disposed"));
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container got disposed"));
        return Task.CompletedTask;
    }
}