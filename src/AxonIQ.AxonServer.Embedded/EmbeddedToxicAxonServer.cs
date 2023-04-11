using System.Diagnostics;
using System.Net;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Toxiproxy.Net;
using Toxiproxy.Net.Toxics;

namespace AxonIQ.AxonServer.Embedded;

public class EmbeddedToxicAxonServer : IToxicAxonServer
{
    private static readonly TimeSpan DefaultMaximumWaitTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultDelayBetweenAttempts = TimeSpan.FromSeconds(1);

    private readonly EmbeddedAxonServer _server;
    private readonly ILogger<EmbeddedToxicAxonServer> _logger;
    private INetworkService? _network;
    private IContainerService? _container;
    private Connection? _proxyConnection;

    public EmbeddedToxicAxonServer(EmbeddedAxonServer server, ILogger<EmbeddedToxicAxonServer> logger)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        _logger.LogDebug("Embedded Toxic Axon Server is being initialized");

        _network = new Builder()
            .UseNetwork($"axon-network-{AxonNetworkCounter.Next()}")
            .Build();

        await _server.StartAsync(_network);

        _container = new Builder()
            .UseContainer()
            .WithName($"toxiproxy-{ToxiProxyCounter.Next()}")
            //.ReuseIfExists()
            .UseImage("ghcr.io/shopify/toxiproxy:2.5.0")
            .RemoveVolumesOnDispose()
            .UseNetwork(_network)
            .ExposePort(8474)
            .ExposePort(8124)
            .WaitForPort("8474/tcp", TimeSpan.FromSeconds(10.0))
            .Build()
            .Start();
        
        _proxyConnection = new Connection("localhost", _container.ToHostExposedEndpoint("8474/tcp").Port, false);
        
        _logger.LogDebug("Embedded Toxic Axon Server got initialized");
    }

    public async Task WaitUntilAvailableAsync(TimeSpan? maximumWaitTime = default, TimeSpan? delayBetweenAttempts = default)
    {
        await _server.WaitUntilAvailableAsync(maximumWaitTime, delayBetweenAttempts);
        
        var attempt = 0;
        var available = false;
        using var client = new HttpClient();
        var endpoint = _container.ToHostExposedEndpoint("8474/tcp");
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = endpoint.Port,
            Path = "version"
        }.Uri;

        var watch = Stopwatch.StartNew();
        while (!available && watch.Elapsed < (maximumWaitTime ?? DefaultMaximumWaitTime))
        {
            _logger.LogDebug("Embedded ToxiProxy Server is being health checked at {Endpoint}",
                requestUri.AbsoluteUri);

            try
            {
                (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                available = true;
            }
            catch (HttpRequestException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded ToxiProxy Server could not be reached at {Endpoint} because {Exception}",
                    requestUri.AbsoluteUri,
                    exception.Message);
                await Task.Delay(delayBetweenAttempts ?? DefaultDelayBetweenAttempts);
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Embedded ToxiProxy Server could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} within {Convert.ToInt32((maximumWaitTime ?? DefaultMaximumWaitTime).TotalSeconds)} seconds and after {attempt} attempts");
        }

        if (_proxyConnection != null)
        {
            await _proxyConnection.Client().ResetAsync();
            
            await _proxyConnection.Client().AddAsync(new Proxy
            {
                Name = "AxonServer",
                Listen = "0.0.0.0:8124",
                Upstream =$"{_server.Properties.NodeSetup.Name}:8124",
                Enabled = true
            });
        }

        _logger.LogDebug("Embedded Toxic Axon Server became available");
    }

    public SystemProperties Properties => _server.Properties;

    public DnsEndPoint GetHttpEndpoint() => _server.GetHttpEndpoint();

    public HttpClient CreateHttpClient() => _server.CreateHttpClient();
    
    public async Task DisableGrpcProxyEndpointAsync()
    {
        if (_proxyConnection != null)
        {
            var proxy = await _proxyConnection.Client().FindProxyAsync("AxonServer");
            proxy.Enabled = false;
            await proxy.UpdateAsync();
        }
    }
    
    public async Task EnableGrpcProxyEndpointAsync()
    {
        if (_proxyConnection != null)
        {
            var proxy = await _proxyConnection.Client().FindProxyAsync("AxonServer");
            proxy.Enabled = true;
            await proxy.UpdateAsync();
        }
    }

    public async Task<IAsyncDisposable> ResetPeerOnGrpcProxyEndpointAsync(int? timeout = default)
    {
        if (_proxyConnection != null)
        {
            var proxy = await _proxyConnection.Client().FindProxyAsync("AxonServer");
            var inputToxic = new ResetPeerToxic();
            if (timeout.HasValue)
            {
                inputToxic.Attributes.Timeout = timeout.Value;
            }
            var toxic = await proxy.AddAsync(inputToxic);
            return new RemoveToxicOnDispose(proxy, toxic);
        }

        return CompletedAsyncDisposable.Instance;
    }
    
    public async Task<IAsyncDisposable> TimeoutEndpointAsync(int? timeout = default)
    {
        if (_proxyConnection != null)
        {
            var proxy = await _proxyConnection.Client().FindProxyAsync("AxonServer");
            var inputToxic = new TimeoutToxic();
            if (timeout.HasValue)
            {
                inputToxic.Attributes.Timeout = timeout.Value;
            }
            var toxic = await proxy.AddAsync(inputToxic);
            return new RemoveToxicOnDispose(proxy, toxic);
        }
        
        return CompletedAsyncDisposable.Instance;
    }

    public DnsEndPoint GetGrpcProxyEndpoint()
    {
        return new DnsEndPoint(
            "localhost",
            _container.ToHostExposedEndpoint("8124/tcp").Port);
    }

    public GrpcChannel CreateGrpcProxyChannel(GrpcChannelOptions? options)
    {
        var address = new UriBuilder
        {
            Host = "localhost",
            Port = _container.ToHostExposedEndpoint("8124/tcp").Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public DnsEndPoint GetGrpcEndpoint() => _server.GetGrpcEndpoint();
    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options) => _server.CreateGrpcChannel(options);
    
    public DnsEndPoint GetContainerGrpcEndpoint() => _server.GetContainerGrpcEndpoint();
    public GrpcChannel CreateContainerGrpcChannel(GrpcChannelOptions? options) =>
        _server.CreateContainerGrpcChannel(options);

    public async Task DisposeAsync()
    {
        _logger.LogDebug("Embedded Toxic Axon Server is being disposed");
        
        await _server.DisposeAsync();
        
        if (_container != null)
        {
            _container.Remove(true);
            _container.Dispose();
        }

        _network?.Dispose();

        _logger.LogDebug("Embedded Toxic Axon Server got disposed");
    }
    
    private class RemoveToxicOnDispose : IAsyncDisposable
    {
        private readonly Proxy _proxy;
        private readonly ToxicBase _toxic;

        public RemoveToxicOnDispose(Proxy proxy, ToxicBase toxic)
        {
            _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
            _toxic = toxic ?? throw new ArgumentNullException(nameof(toxic));
        }
    
        public async ValueTask DisposeAsync() => await _proxy.RemoveToxicAsync(_toxic.Name);
    }
    
    private class CompletedAsyncDisposable : IAsyncDisposable
    {
        public static IAsyncDisposable Instance = new CompletedAsyncDisposable();
        
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

