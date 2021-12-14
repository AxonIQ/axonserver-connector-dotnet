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
using YamlDotNet.Serialization;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class EmbeddedAxonCluster : IAxonCluster
{
    private readonly ILogger<EmbeddedAxonCluster> _logger;
    private IContainerService[]? _nodes;
    private DirectoryInfo[]? _nodeFiles;

    private EmbeddedAxonCluster(SystemProperties[] nodeProperties, string license, ClusterTemplate? clusterTemplate, ILogger<EmbeddedAxonCluster> logger)
    {
        NodeProperties = nodeProperties ?? throw new ArgumentNullException(nameof(nodeProperties));
        License = license ?? throw new ArgumentNullException(nameof(license));
        ClusterTemplate = clusterTemplate;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SystemProperties[] NodeProperties { get; }
    public string License { get; }
    public ClusterTemplate? ClusterTemplate { get; }

    public async Task InitializeAsync()
    {
        _logger.LogDebug("Embedded Axon Cluster is being initialized");

        _nodeFiles = new DirectoryInfo[NodeProperties.Length];
        _nodes = new IContainerService[NodeProperties.Length];
        for (var index = 0; index < NodeProperties.Length; index++)
        {
            _nodeFiles[index] = new DirectoryInfo(
                Path.Combine(Path.GetTempPath(), shortid.ShortId.Generate(new GenerationOptions
                {
                    UseSpecialCharacters = false
                })));
            _nodeFiles[index].Create();
            
            if (ClusterTemplate != null)
            {
                var template = new Serializer().Serialize(ClusterTemplate.Serialize());
                await File.WriteAllTextAsync(Path.Combine(_nodeFiles[index].FullName, "cluster-template.yml"), template);    
            }
            await File.WriteAllTextAsync(Path.Combine(_nodeFiles[index].FullName, "axoniq.license"), License);
            await File.WriteAllTextAsync(Path.Combine(_nodeFiles[index].FullName, "axonserver.properties"), string.Join(Environment.NewLine, NodeProperties[index].Serialize()));
            
            _nodes[index] = new Builder()
                .UseContainer()
                .UseImage("axoniq/axonserver-enterprise")
                .ExposePort(8024)
                .ExposePort(8124)
                .Mount(_nodeFiles[index].FullName, "/axonserver/config", MountType.ReadOnly)
                .WaitForPort("8024/tcp", TimeSpan.FromSeconds(10.0))
                .Build()
                .Start();
        }

        _logger.LogDebug("Embedded Axon Cluster got started");
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var endpoint = _nodes[0].ToHostExposedEndpoint("8024/tcp");
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
            _logger.LogDebug("Embedded Axon Cluster is being health checked at {Endpoint}",
                requestUri.AbsoluteUri);

            try
            {
                var response = (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var document = JsonDocument.Parse(json);
                var property = document.RootElement.GetProperty("status");
                if (property.GetString()?.ToLowerInvariant() == "up")
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
                    "Embedded Axon Cluster actuator health does not contain a 'status' property with the value 'up'");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            catch (HttpRequestException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Embedded Axon Cluster could not be reached at {Endpoint} because {Exception}",
                    requestUri.AbsoluteUri,
                    exception.Message);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            attempt++;
        }

        if (!available)
        {
            throw new InvalidOperationException(
                $"Embedded Axon Cluster could not be initialized. Failed to reach it at {requestUri.AbsoluteUri} after {maximumAttempts} attempts");
        }

        _logger.LogDebug("Embedded Axon Cluster became available");
        _logger.LogDebug("Embedded Axon Cluster got initialized");
    }

    public DnsEndPoint[] GetHttpEndpoints()
    {
        if (_nodes == null)
        {
            throw new InvalidOperationException("The cluster nodes have not been initialized");
        }

        return Array.ConvertAll(
            _nodes,
            clusterNode => new DnsEndPoint(
                "localhost",
                clusterNode.ToHostExposedEndpoint("8024/tcp").Port
            )
        );
    }

    public HttpClient CreateHttpClient(int node)
    {
        if (_nodes == null)
        {
            throw new InvalidOperationException("The cluster nodes have not been initialized");
        }
        if (node < 0 || node >= _nodes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(node), node,
                $"The node index is out of the acceptable range: [0,{_nodes.Length}]");
        }

        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = "localhost",
                Port = _nodes[node].ToHostExposedEndpoint("8024/tcp").Port
            }.Uri
        };
    }

    public DnsEndPoint[] GetGrpcEndpoints()
    {
        if (_nodes == null)
        {
            throw new InvalidOperationException("The cluster nodes have not been initialized");
        }

        return Array.ConvertAll(
            _nodes,
            clusterNode => new DnsEndPoint(
                "localhost",
                clusterNode.ToHostExposedEndpoint("8124/tcp").Port
            )
        );
    }

    public GrpcChannel CreateGrpcChannel(int node, GrpcChannelOptions? options)
    {
        if (_nodes == null)
        {
            throw new InvalidOperationException("The cluster nodes have not been initialized");
        }
        
        var address = new UriBuilder
        {
            Host = "localhost",
            Port = _nodes[node].ToHostExposedEndpoint("8124/tcp").Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public Task DisposeAsync()
    {
        _logger.LogDebug("Embedded Axon Cluster is being disposed");
        
        if (_nodes != null)
        {
            Array.ForEach(_nodes, node =>
            {
                node.Remove(true);
                node.Dispose();
            });
        }

        if (_nodeFiles != null)
        {
            Array.ForEach(_nodeFiles, files => files.Delete(true));
        }

        _logger.LogDebug("Embedded Axon Cluster got disposed");
        return Task.CompletedTask;
    }

    public static IAxonCluster WithAccessControlDisabled(ILogger<EmbeddedAxonCluster> logger)
    {
        var cluster = new SystemProperties
        {
            ClusterSetup =
            {
                AutoclusterFirst = "axonserver-1",
                AutoclusterContexts = new[] { "_admin", "default" }
            },
            AccessControl =
            {
                AccessControlEnabled = false,
                AccessControlInternalToken = Guid.NewGuid().ToString("N")
            }
        }; 
        var node1 = cluster.Clone();
        node1.NodeSetup.Name = "axonserver-1";
        node1.NodeSetup.Hostname = "axonserver-1";
        node1.NodeSetup.InternalHostname = "axonserver-1";
        var node2 = cluster.Clone();
        node2.NodeSetup.Name = "axonserver-2";
        node2.NodeSetup.Hostname = "axonserver-2";
        node2.NodeSetup.InternalHostname = "axonserver-2";
        var node3 = cluster.Clone();
        node3.NodeSetup.Name = "axonserver-3";
        node3.NodeSetup.Hostname = "axonserver-3";
        node3.NodeSetup.InternalHostname = "axonserver-3";
        var nodeProperties = new[] { node1, node2, node3 };
        return new EmbeddedAxonCluster(nodeProperties, "", null, logger);
    }

    public static IAxonCluster WithAccessControlEnabled(ILogger<EmbeddedAxonCluster> logger)
    {
        var cluster = new SystemProperties
        {
            ClusterSetup =
            {
                AutoclusterFirst = "axonserver-1",
                AutoclusterContexts = new[] { "_admin", "default" }
            },
            AccessControl =
            {
                AccessControlEnabled = true,
                AccessControlInternalToken = Guid.NewGuid().ToString("N"),
                AccessControlSystemToken = Guid.NewGuid().ToString("N")
            }
        };
        var node1 = cluster.Clone();
        node1.NodeSetup.Name = "axonserver-1";
        node1.NodeSetup.Hostname = "axonserver-1";
        node1.NodeSetup.InternalHostname = "axonserver-1";
        var node2 = cluster.Clone();
        node2.NodeSetup.Name = "axonserver-2";
        node2.NodeSetup.Hostname = "axonserver-2";
        node2.NodeSetup.InternalHostname = "axonserver-2";
        var node3 = cluster.Clone();
        node3.NodeSetup.Name = "axonserver-3";
        node3.NodeSetup.Hostname = "axonserver-3";
        node3.NodeSetup.InternalHostname = "axonserver-3";
        var nodeProperties = new[] { node1, node2, node3 };
        var template = new ClusterTemplate
        {
            First = "axonserver-1",
            Applications = new ClusterTemplateApplication[]
            {
                new()
                {
                    Name = "axonserver-dotnet-connector-tests",
                    Roles = new ClusterTemplateApplicationRole[]
                    {
                        new()
                        {
                            Context = "default",
                            Roles = new[] { "USE_CONTEXT" }
                        }
                    },
                    Token = Guid.NewGuid().ToString("N")
                }
            },
            ReplicationGroups = new ClusterTemplateReplicationGroup[]
            {
                new()
                {
                    Name = "default",
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = "default"
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-2",
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = "axonserver-3",
                            Role = "PRIMARY"
                        }
                    }
                },
                new()
                {
                    Name = "_admin",
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = "_admin"
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-1",
                            Role = "PRIMARY"
                        }
                    }
                }
            }
        };
        return new EmbeddedAxonCluster(nodeProperties, "", template, logger);
    }
}