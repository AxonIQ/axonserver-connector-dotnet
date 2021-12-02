using System.Net;
using System.Net.Http.Headers;
using Grpc.Net.Client;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

/// <summary>
/// Manages the interaction with a container composed in the CI environment.
/// </summary>
public abstract class ComposedAxonServerContainer : IAxonServerContainer
{
    private readonly IMessageSink _logger;

    protected ComposedAxonServerContainer(IMessageSink logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected abstract int HttpPort { get; }
    protected abstract int GrpcPort { get; }

    public async Task InitializeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is being initialized"));
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = HttpPort,
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
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container got initialized"));
    }

    public DnsEndPoint GetHttpEndpoint()
    {
        return new DnsEndPoint(
            "localhost",
            int.Parse(Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_PORT")!));
    }

    public HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = "localhost",
                Port = HttpPort
            }.Uri
        };
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return new DnsEndPoint(
            "localhost",
            GrpcPort
        );
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options)
    {
        var address = new UriBuilder
        {
            Host = "localhost",
            Port = GrpcPort
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public Task DisposeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container is being disposed"));
        _logger.OnMessage(new DiagnosticMessage("Composed Axon Server Container got disposed"));
        return Task.CompletedTask;
    }

    public static IAxonServerContainerWithAccessControlDisabled WithAccessControlDisabled(IMessageSink logger)
    {
        return new ComposedAxonServerContainerWithAccessControlDisabled(logger);
    }

    public static IAxonServerContainerWithAccessControlEnabled WithAccessControlEnabled(IMessageSink logger)
    {
        return new ComposedAxonServerContainerWithAccessControlEnabled(logger);
    }

    private class ComposedAxonServerContainerWithAccessControlDisabled : ComposedAxonServerContainer,
        IAxonServerContainerWithAccessControlDisabled
    {
        public ComposedAxonServerContainerWithAccessControlDisabled(IMessageSink logger) : base(logger)
        {
            if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_PORT") == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_PORT environment variable is missing.");
            }

            if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_GRPC_PORT") == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_GRPC_PORT environment variable is missing.");
            }

            HttpPort = int.Parse(Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_PORT")!);
            GrpcPort = int.Parse(
                Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_DISABLED_GRPC_PORT")!);
        }

        protected override int HttpPort { get; }
        protected override int GrpcPort { get; }
    }

    private class ComposedAxonServerContainerWithAccessControlEnabled : ComposedAxonServerContainer,
        IAxonServerContainerWithAccessControlEnabled
    {
        public ComposedAxonServerContainerWithAccessControlEnabled(IMessageSink logger) : base(logger)
        {
            if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_PORT") == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_PORT environment variable is missing.");
            }

            if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_GRPC_PORT") == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_GRPC_PORT environment variable is missing.");
            }

            if (Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_TOKEN") == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_AXONSERVER_ACCESSCONTROL_TOKEN environment variable is missing.");
            }

            HttpPort = int.Parse(Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_PORT")!);
            GrpcPort = int.Parse(
                Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED_GRPC_PORT")!);
            Token = Environment.GetEnvironmentVariable("AXONIQ_AXONSERVER_ACCESSCONTROL_TOKEN")!;
        }

        protected override int HttpPort { get; }
        protected override int GrpcPort { get; }
        public string Token { get; }
    }
}