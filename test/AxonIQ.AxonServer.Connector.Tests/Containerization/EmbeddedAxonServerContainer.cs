using System.Net;
using System.Net.Http.Headers;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Grpc.Net.Client;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class EmbeddedAxonServerContainer : IAxonServerContainer
{
    private readonly IMessageSink _logger;
    private IContainerService? _container;

    public EmbeddedAxonServerContainer(IMessageSink logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected abstract string[] ContainerEnvironmentVariables { get; }

    public async Task InitializeAsync()
    {
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container is being initialized"));
        _container = new Builder()
            .UseContainer()
            .UseImage("axoniq/axonserver")
            .ExposePort(8024)
            .ExposePort(8124)
            .WithEnvironment(ContainerEnvironmentVariables)
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
        const int maximumAttempts = 60;
        var attempt = 0;
        while (!available && attempt < maximumAttempts)
        {
            _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container is being health checked at {0}",
                requestUri.AbsoluteUri));

            try
            {
                (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                available = true;
            }
            catch (HttpRequestException exception)
            {
                _logger.OnMessage(new DiagnosticMessage(
                    "Embedded Axon Server Container could not be reached at {0} because {1}",
                    requestUri.AbsoluteUri,
                    exception));
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Embedded Axon Server Container could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} after {maximumAttempts} attempts");
        }

        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container became available"));
        _logger.OnMessage(new DiagnosticMessage("Embedded Axon Server Container got initialized"));
    }

    public DnsEndPoint GetHttpEndpoint()
    {
        return new DnsEndPoint(
            "localhost",
            _container.ToHostExposedEndpoint("8024/tcp").Port);
    }

    public HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = "localhost",
                Port = _container.ToHostExposedEndpoint("8024/tcp").Port
            }.Uri
        };
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return new DnsEndPoint(
            "localhost",
            _container.ToHostExposedEndpoint("8124/tcp").Port);
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options)
    {
        var address = new UriBuilder
        {
            Host = "localhost",
            Port = _container.ToHostExposedEndpoint("8124/tcp").Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
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

    public static IAxonServerContainerWithAccessControlDisabled WithAccessControlDisabled(IMessageSink logger)
    {
        return new EmbeddedAxonServerContainerWithAccessControlDisabled(logger);
    }

    public static IAxonServerContainerWithAccessControlEnabled WithAccessControlEnabled(IMessageSink logger)
    {
        return new EmbeddedAxonServerContainerWithAccessControlEnabled(logger);
    }

    private class EmbeddedAxonServerContainerWithAccessControlDisabled : EmbeddedAxonServerContainer,
        IAxonServerContainerWithAccessControlDisabled
    {
        public EmbeddedAxonServerContainerWithAccessControlDisabled(IMessageSink logger) : base(logger)
        {
        }

        protected override string[] ContainerEnvironmentVariables { get; } =
        {
            "AXONIQ_AXONSERVER_NAME=axonserver",
            "AXONIQ_AXONSERVER_HOSTNAME=localhost",
            "AXONIQ_AXONSERVER_DEVMODE_ENABLED=true",
            "AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED=false"
        };
    }

    private class EmbeddedAxonServerContainerWithAccessControlEnabled : EmbeddedAxonServerContainer,
        IAxonServerContainerWithAccessControlEnabled
    {
        public EmbeddedAxonServerContainerWithAccessControlEnabled(IMessageSink logger) : base(logger)
        {
            Token = Guid.NewGuid().ToString("N");
        }

        public string Token { get; }

        protected override string[] ContainerEnvironmentVariables
        {
            get
            {
                return new[]
                {
                    "AXONIQ_AXONSERVER_NAME=axonserver",
                    "AXONIQ_AXONSERVER_HOSTNAME=localhost",
                    "AXONIQ_AXONSERVER_DEVMODE_ENABLED=true",
                    "AXONIQ_AXONSERVER_ACCESSCONTROL_ENABLED=true",
                    "AXONIQ_AXONSERVER_ACCESSCONTROL_TOKEN=" + Token
                };
            }
        }
    }
}