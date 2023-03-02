﻿using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Model.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using shortid.Configuration;

namespace AxonIQ.AxonServer.Embedded;

public class EmbeddedAxonServer : IAxonServer
{
    private static readonly TimeSpan DefaultMaximumWaitTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultDelayBetweenAttempts = TimeSpan.FromSeconds(1);
    
    private readonly ILogger<EmbeddedAxonServer> _logger;
    private IContainerService? _container;
    private DirectoryInfo? _serverFiles;

    public EmbeddedAxonServer(SystemProperties properties, ILogger<EmbeddedAxonServer> logger)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SystemProperties Properties { get; }
    
    public bool EmitServerLogsOnDispose { get; set; }

    public async Task InitializeAsync()
    {
        _logger.LogDebug("Embedded Axon Server is being initialized");
        await StartAsync(null);
        _logger.LogDebug("Embedded Axon Server got initialized");
    }

    internal async Task StartAsync(INetworkService? network)
    {
        _logger.LogDebug("Embedded Axon Server is being started");
        _serverFiles = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), shortid.ShortId.Generate(new GenerationOptions(useSpecialCharacters: false))));
        _serverFiles.Create();
        
        await File.WriteAllTextAsync(Path.Combine(_serverFiles.FullName, "axonserver.properties"), string.Join(Environment.NewLine, Properties.Serialize()));

        var builder = new Builder()
            .UseContainer()
            .ReuseIfExists()
            .UseImage("axoniq/axonserver:latest")
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

        if (network != null)
        {
            builder.UseNetwork(network);
        }

        _container = builder.Build().Start();
        
        _logger.LogDebug("Embedded Axon Server got started");
    }

    public async Task WaitUntilAvailableAsync(TimeSpan? maximumWaitTime = default, TimeSpan? delayBetweenAttempts = default)
    {
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
        while (!available && watch.Elapsed < (maximumWaitTime ?? DefaultMaximumWaitTime))
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
                    await Task.Delay(delayBetweenAttempts ?? DefaultDelayBetweenAttempts);
                }
            }
            catch (KeyNotFoundException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded Axon Server actuator health does not contain a 'status' property with the value 'UP'");
                await Task.Delay(delayBetweenAttempts ?? DefaultDelayBetweenAttempts);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded Axon Server could not be reached at {Endpoint} because {Exception}",
                    requestUri.AbsoluteUri,
                    exception.Message);
                await Task.Delay(delayBetweenAttempts ?? DefaultDelayBetweenAttempts);
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Embedded Axon Server could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} within {Convert.ToInt32((maximumWaitTime ?? DefaultMaximumWaitTime).TotalSeconds)} seconds and after {attempt} attempts");
        }

        _logger.LogDebug("Embedded Axon Server became available");
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
    
    public DnsEndPoint GetContainerGrpcEndpoint()
    {
        return new DnsEndPoint(
            _container?.GetConfiguration(true).NetworkSettings.IPAddress ?? "localhost",
            8124);
    }

    public GrpcChannel CreateContainerGrpcChannel(GrpcChannelOptions? options)
    {
        var address = new UriBuilder
        {
            Host = _container?.GetConfiguration(true).NetworkSettings.IPAddress ?? "localhost",
            Port = 8124
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public Task DisposeAsync()
    {
        _logger.LogDebug("Embedded Axon Server is being disposed");
        if (_container != null)
        {
            if (EmitServerLogsOnDispose)
            {
                _logger.LogDebug("Embedded Axon Server logs");
                foreach (var line in _container.Logs().ReadToEnd())
                {
                    // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
                    _logger.LogDebug(line);
                }
            }

            _container.Remove(true);
            _container.Dispose();
        }

        _logger.LogDebug("Embedded Axon Server got disposed");
        return Task.CompletedTask;
    }

    public static EmbeddedAxonServer WithAccessControlDisabled(ILogger<EmbeddedAxonServer> logger, bool emitServerLogs = false)
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
            },
            Logging =
            {
                LogLevels = new []
                {
                    new KeyValuePair<string, string>("io.axoniq.axonserver", "DEBUG") 
                }
            }
        };
        return new EmbeddedAxonServer(properties, logger)
        {
            EmitServerLogsOnDispose = emitServerLogs
        };
    }

    public static EmbeddedAxonServer WithAccessControlEnabled(ILogger<EmbeddedAxonServer> logger, bool emitServerLogs = false)
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
        return new EmbeddedAxonServer(properties, logger)
        {
            EmitServerLogsOnDispose = emitServerLogs
        };
    }
}