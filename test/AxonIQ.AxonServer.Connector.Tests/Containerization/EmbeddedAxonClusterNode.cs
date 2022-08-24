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
using YamlDotNet.RepresentationModel;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class EmbeddedAxonClusterNode : IAxonClusterNode
{
    private readonly ILogger _logger;
    private IContainerService? _container;

    public EmbeddedAxonClusterNode(SystemProperties properties, ClusterTemplate template, ILogger logger)
    {
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Files = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), shortid.ShortId.Generate(new GenerationOptions(useSpecialCharacters: false))));
    }

    public SystemProperties Properties { get; }
    public ClusterTemplate Template { get; }
    public DirectoryInfo Files { get; }

    public DnsEndPoint GetHttpEndpoint()
    {
        if (_container == null)
        {
            throw new InvalidOperationException("The cluster node have not been initialized");
        }

        if (Properties.NodeSetup.ServerPort.HasValue)
        {
            return new DnsEndPoint(
                 Properties.NodeSetup.Hostname ?? "localhost",
                _container.ToHostExposedEndpoint($"{Properties.NodeSetup.ServerPort.Value}/tcp").Port
            );    
        }
        return new DnsEndPoint(
            Properties.NodeSetup.Hostname ?? "localhost",
            _container.ToHostExposedEndpoint("8024/tcp").Port
        );
    }

    public HttpClient CreateHttpClient()
    {
        if (_container == null)
        {
            throw new InvalidOperationException("The cluster node have not been initialized");
        }

        var endpoint = GetHttpEndpoint();
        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = endpoint.Host,
                Port = endpoint.Port
            }.Uri
        };
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        if (_container == null)
        {
            throw new InvalidOperationException("The cluster node have not been initialized");
        }
        
        if (Properties.NodeSetup.Port.HasValue)
        {
            return new DnsEndPoint(
                Properties.NodeSetup.Hostname ?? "localhost",
                _container.ToHostExposedEndpoint($"{Properties.NodeSetup.Port.Value}/tcp").Port
            );    
        }
        return new DnsEndPoint(
            Properties.NodeSetup.Hostname ?? "localhost",
            _container.ToHostExposedEndpoint("8124/tcp").Port
        );
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options)
    {
        if (_container == null)
        {
            throw new InvalidOperationException("The cluster node have not been initialized");
        }
        
        var endpoint = GetGrpcEndpoint();
        var address = new UriBuilder
        {
            Host = endpoint.Host,
            Port = endpoint.Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    internal Context[] ScanForContexts()
    {
        var contexts = new HashSet<Context>();

        foreach (var context in Properties.ScanForContexts())
        {
            contexts.Add(context);
        }

        foreach (var context in Template.ScanForContexts())
        {
            contexts.Add(context);
        }

        return contexts.ToArray();
    }
    
    internal void Start(INetworkService? network)
    {
        Files.Create();

        var stream = new YamlStream(Template.Serialize());
        using (var writer = new StringWriter())
        {
            stream.Save(writer, false);
            File.WriteAllText(Path.Combine(Files.FullName, "cluster-template.yml"), writer.ToString());
        }
        File.WriteAllText(Path.Combine(Files.FullName, "axoniq.license"), AxonClusterLicense.FromEnvironment());
        File.WriteAllText(Path.Combine(Files.FullName, "axonserver.properties"),
            string.Join(Environment.NewLine, Properties.Serialize()));

        var builder = new Builder()
            .UseContainer()
            .UseImage("axoniq/axonserver-enterprise:latest-dev")
            .Mount(Files.FullName, "/axonserver/config", MountType.ReadOnly)
            .WaitForPort("8024/tcp", TimeSpan.FromSeconds(10.0));
        if (Properties.NodeSetup.Port.HasValue)
        {
            builder.ExposePort(Properties.NodeSetup.Port.Value, Properties.NodeSetup.Port.Value);
        }
        else
        {
            builder.ExposePort(8124);
        }
        if (Properties.NodeSetup.ServerPort.HasValue)
        {
            builder.ExposePort(Properties.NodeSetup.ServerPort.Value, Properties.NodeSetup.ServerPort.Value);
        }
        else
        {
            builder.ExposePort(8024);
        }
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
    }

    internal async Task WaitUntilAvailableAsync(int cluster)
    {
        if (_container == null)
            throw new InvalidOperationException("The cluster node has not been initialized");
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var maximumWaitTime = TimeSpan.FromMinutes(2);
        var attempt = 0;
        var available = false;
        var endpoint = _container.ToHostExposedEndpoint("8024/tcp");
        var requestUri = new UriBuilder
        {
            Host = "localhost",
            Port = endpoint.Port,
            Path = "actuator/health"
        }.Uri;
        
        //Only test the raft status of contexts this node is hosting a replication group for 
        var contexts =
            Template
                .ReplicationGroups?
                .Where(replicationGroup =>
                    replicationGroup.Roles?.Any(role => role.Node == Properties.NodeSetup.Name) ?? false)
                .SelectMany(replicationGroup => replicationGroup.Contexts?.Where(context => context.Name != null)
                    .Select(context => new Context(context.Name!)) ?? Array.Empty<Context>())
                .ToArray() ?? Array.Empty<Context>();
            

        var watch = Stopwatch.StartNew();
        while (!available && watch.Elapsed < maximumWaitTime)
        {
            _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster is being health checked on node {Node} at {Endpoint}",
                cluster,
                _container.Name,
                requestUri.AbsoluteUri);

            try
            {
                var response = (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var document = JsonDocument.Parse(json);
                if (document.RootElement.GetProperty("status").GetString() == "UP" &&
                    document.RootElement.GetProperty("components").GetProperty("raft").GetProperty("status").GetString() == "UP" &&
                    contexts.All(context => 
                        document.RootElement
                            .GetProperty("components").GetProperty("raft").GetProperty("details")
                            .GetProperty($"{context.ToString()}.leader").GetString() != null 
                    ))
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
                    "[{ClusterId}]Embedded Axon Cluster actuator health does not contain have a 'status' or a 'components.raft.status' property with the value 'UP'",
                    cluster);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            catch (HttpRequestException exception)
            {
                _logger.LogDebug(
                    exception,
                    "[{ClusterId}]Embedded Axon Cluster could not be reached on node {Node} at {Endpoint} because {Exception}",
                    cluster,
                    _container.Name,
                    requestUri.AbsoluteUri,
                    exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"[{cluster}]Embedded Axon Cluster could not be initialized. Failed to reach node {_container.Name} at {requestUri.AbsoluteUri} within {Convert.ToInt32(maximumWaitTime.TotalSeconds)} seconds and after {attempt} attempts");
        }
    }

    internal void Stop(INetworkService? network)
    {
        if(_container != null)
        {
            network?.Detach(_container);

            _container.Remove(true);
            _container.Dispose();
        }
        
        if (Files.Exists)
        {
            Files.Delete(true);
        }
    }
}