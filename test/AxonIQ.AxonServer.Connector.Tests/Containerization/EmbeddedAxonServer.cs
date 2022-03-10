using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Model.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using shortid.Configuration;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class EmbeddedAxonServer : IAxonServer
{
    private readonly ILogger<EmbeddedAxonServer> _logger;
    private IContainerService? _container;
    private DirectoryInfo? _serverFiles;

    public EmbeddedAxonServer(SystemProperties properties, ILogger<EmbeddedAxonServer> logger)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SystemProperties Properties { get; }

    public async Task InitializeAsync()
    {
        _logger.LogDebug("Embedded Axon Server is being initialized");
        
        _serverFiles = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), shortid.ShortId.Generate(new GenerationOptions
            {
                UseSpecialCharacters = false
            })));
        _serverFiles.Create();
        
        await File.WriteAllTextAsync(Path.Combine(_serverFiles.FullName, "axonserver.properties"), string.Join(Environment.NewLine, Properties.Serialize()));

        var builder = new Builder()
            .UseContainer()
            .UseImage("axoniq/axonserver")
            .RemoveVolumesOnDispose()
            .ExposePort(8024)
            .ExposePort(8124)
            .Mount(_serverFiles.FullName, "/config", MountType.ReadOnly)
            .WaitForPort("8024/tcp", TimeSpan.FromSeconds(10.0));
        if (!string.IsNullOrEmpty(Properties.NodeSetup.Name))
        {
            builder.WithName(Properties.NodeSetup.Name);
        }

        if (!string.IsNullOrEmpty(Properties.NodeSetup.Hostname))
        {
            builder.WithHostName(Properties.NodeSetup.Hostname);
        }

        _container = builder.Build().Start();
        
        _logger.LogDebug("Embedded Axon Server got started");
        
        var maximumWaitTime = TimeSpan.FromMinutes(2);
        var attempt = 0;
        var available = false;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var endpoint = _container.ToHostExposedEndpoint("8024/tcp");
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = endpoint.Port,
            Path = "actuator/health"
        }.Uri;

        var watch = Stopwatch.StartNew();
        while (!available && watch.Elapsed < maximumWaitTime)
        {
            _logger.LogDebug("Embedded Axon Server is being health checked at {Endpoint}",
                requestUri.AbsoluteUri);

            try
            {
                var response = (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var document = JsonDocument.Parse(json);
                var property = document.RootElement.GetProperty("status");
                if (property.GetString() == "UP")
                {
                    available = true;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            catch (KeyNotFoundException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded Axon Server actuator health does not contain a 'status' property with the value 'UP'");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            catch (HttpRequestException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded Axon Server could not be reached at {Endpoint} because {Exception}",
                    requestUri.AbsoluteUri,
                    exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Embedded Axon Server could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} within {Convert.ToInt32(maximumWaitTime.TotalSeconds)} seconds and after {attempt} attempts");
        }

        _logger.LogDebug("Embedded Axon Server became available");
        _logger.LogDebug("Embedded Axon Server got initialized");
    }

    public DnsEndPoint GetHttpEndpoint()
    {
        return new DnsEndPoint(
            Properties.NodeSetup.Hostname ?? "localhost",
            _container.ToHostExposedEndpoint("8024/tcp").Port);
    }

    public HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = Properties.NodeSetup.Hostname ?? "localhost",
                Port = _container.ToHostExposedEndpoint("8024/tcp").Port
            }.Uri
        };
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return new DnsEndPoint(
            Properties.NodeSetup.Hostname ?? "localhost",
            _container.ToHostExposedEndpoint("8124/tcp").Port);
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options)
    {
        var address = new UriBuilder
        {
            Host = Properties.NodeSetup.Hostname ?? "localhost",
            Port = _container.ToHostExposedEndpoint("8124/tcp").Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public Task DisposeAsync()
    {
        _logger.LogDebug("Embedded Axon Server is being disposed");
        if (_container != null)
        {
            _container.Remove(true);
            _container.Dispose();
        }

        _logger.LogDebug("Embedded Axon Server got disposed");
        return Task.CompletedTask;
    }

    public static IAxonServer WithAccessControlDisabled(ILogger<EmbeddedAxonServer> logger)
    {
        var suffix = AxonServerCounter.Next();
        var properties = new SystemProperties
        {
            NodeSetup =
            {
                Name = $"axonserver-{suffix}",
                Hostname = "localhost",
                DevModeEnabled = true
            },
            AccessControl =
            {
                AccessControlEnabled = false
            },
            KeepAlive =
            {
                HeartbeatEnabled = true
            }
        };
        return new EmbeddedAxonServer(properties, logger);
    }

    public static IAxonServer WithAccessControlEnabled(ILogger<EmbeddedAxonServer> logger)
    {
        var suffix = AxonServerCounter.Next();
        var properties = new SystemProperties
        {
            NodeSetup =
            {
                Name = $"axonserver-{suffix}",
                Hostname = "localhost",
                DevModeEnabled = true
            },
            AccessControl =
            {
                AccessControlEnabled = true,
                AccessControlToken = Guid.NewGuid().ToString("N"),
                AccessControlAdminToken = Guid.NewGuid().ToString("N")
            },
            KeepAlive =
            {
                HeartbeatEnabled = true
            }
        };
        return new EmbeddedAxonServer(properties, logger);
    }
}