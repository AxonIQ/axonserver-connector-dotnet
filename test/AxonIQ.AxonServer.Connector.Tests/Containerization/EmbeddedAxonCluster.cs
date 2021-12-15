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
    private readonly int _id = EmbeddedAxonClusterCounter.Next();
    
    private readonly EmbeddedAxonClusterNode[] _nodes;
    private readonly ClusterTemplate _clusterTemplate;
    private readonly ILogger<EmbeddedAxonCluster> _logger;

    private Context[]? _contexts;
    private INetworkService? _network;
    private IContainerService[]? _containers;

    private EmbeddedAxonCluster(EmbeddedAxonClusterNode[] nodes, ClusterTemplate clusterTemplate, ILogger<EmbeddedAxonCluster> logger)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _clusterTemplate = clusterTemplate ?? throw new ArgumentNullException(nameof(clusterTemplate));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAxonClusterNode[] Nodes => _nodes
        .Select<EmbeddedAxonClusterNode, IAxonClusterNode>(node => node)
        .ToArray();

    public Context[] Contexts
    {
        get
        {
            if (_contexts == null)
            {
                var contexts = new HashSet<Context>();

                foreach (var node in Nodes)
                {
                    foreach (var context in node.Properties.ScanForContexts())
                    {
                        contexts.Add(context);
                    }
                }

                foreach (var context in _clusterTemplate.ScanForContexts())
                {
                    contexts.Add(context);
                }

                _contexts = contexts.ToArray();
            }

            return _contexts;
        }
    }
    
    private string License
    {
        get
        {
            var licensePath = Environment.GetEnvironmentVariable("AXONIQ_LICENSE");
            if (licensePath == null)
            {
                throw new InvalidOperationException(
                    "The AXONIQ_LICENSE environment variable was not set. It is required if you want to interact with an embedded axon cluster.");
            }

            if (!File.Exists(licensePath))
            {
                throw new InvalidOperationException(
                    $"The AXONIQ_LICENSE environment variable value refers to a file path that does not exist: {licensePath}");
            }

            return File.ReadAllText(licensePath);
        }
    }

    public async Task InitializeAsync()
    {
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster is being initialized", _id);

        _network = ProvisionNetwork();
        _containers = ProvisionClusterNodes();

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got started", _id);
        
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        const int maximumAttempts = 60;
        var attempt = 0;
        foreach (var container in _containers)
        {
            var endpoint = _containers[0].ToHostExposedEndpoint("8024/tcp");
            var requestUri = new UriBuilder
            {
                Host = "localhost",
                Port = endpoint.Port,
                Path = "actuator/health"
            }.Uri;

            var available = false;
            while (!available && attempt < maximumAttempts)
            {
                _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster is being health checked on node {Node} at {Endpoint}",
                    _id,
                    container.Name,
                    requestUri.AbsoluteUri);

                try
                {
                    var response = (await client.GetAsync(requestUri)).EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var document = JsonDocument.Parse(json);
                    if (document.RootElement.GetProperty("status").GetString() == "UP" &&
                        document.RootElement.GetProperty("components").GetProperty("raft").GetProperty("status").GetString() == "UP" &&
                        Contexts.All(context => 
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
                        _id);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogDebug(
                        exception,
                        "[{ClusterId}]Embedded Axon Cluster could not be reached on node {Node} at {Endpoint} because {Exception}",
                        _id,
                        container.Name,
                        requestUri.AbsoluteUri,
                        exception.Message);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                attempt++;
            }

            if (attempt == maximumAttempts)
            {
                throw new InvalidOperationException(
                    $"[{_id}]Embedded Axon Cluster could not be initialized. Failed to reach node {container.Name} at {requestUri.AbsoluteUri} after {maximumAttempts} attempts");
            }
        }

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster became available", _id);
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got initialized", _id);
    }
    
    private INetworkService ProvisionNetwork()
    {
        return new Builder().UseNetwork($"axoniq-dotnet-{_id}").ReuseIfExist().Build();
    }

    private IContainerService[] ProvisionClusterNodes()
    {
        var containers = new IContainerService[_nodes.Length];
        for (var index = 0; index < _nodes.Length; index++)
        {
            var node = _nodes[index];
            node.Files.Create();

            var template = new Serializer().Serialize(_clusterTemplate.Serialize());
            File.WriteAllText(Path.Combine(node.Files.FullName, "cluster-template.yml"), template);
            File.WriteAllText(Path.Combine(node.Files.FullName, "axoniq.license"), License);
            File.WriteAllText(Path.Combine(node.Files.FullName, "axonserver.properties"),
                string.Join(Environment.NewLine, node.Properties.Serialize()));

            var builder = new Builder()
                .UseContainer()
                .UseImage("axoniq/axonserver-enterprise:latest-dev")
                .UseNetwork(_network)
                .ExposePort(8024)
                .ExposePort(8124)
                .Mount(node.Files.FullName, "/axonserver/config", MountType.ReadOnly)
                .WaitForPort("8024/tcp", TimeSpan.FromSeconds(10.0));
            if (!string.IsNullOrEmpty(node.Properties.NodeSetup.Name))
            {
                builder.WithName(node.Properties.NodeSetup.Name);
            }

            if (!string.IsNullOrEmpty(node.Properties.NodeSetup.Hostname))
            {
                builder.WithHostName(node.Properties.NodeSetup.Hostname);
            }

            containers[index] = builder.Build().Start();
        }

        return containers;
    }

    public DnsEndPoint[] GetHttpEndpoints()
    {
        if (_containers == null)
        {
            throw new InvalidOperationException($"[{_id}]The cluster nodes have not been initialized");
        }

        return Array.ConvertAll(
            _containers,
            clusterNode => new DnsEndPoint(
                "localhost",
                clusterNode.ToHostExposedEndpoint("8024/tcp").Port
            )
        );
    }

    public HttpClient CreateHttpClient(int node)
    {
        if (_containers == null)
        {
            throw new InvalidOperationException($"[{_id}]The cluster nodes have not been initialized");
        }
        if (node < 0 || node >= _containers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(node), node,
                $"[{_id}]The node index is out of the acceptable range: [0,{_containers.Length}]");
        }

        return new HttpClient
        {
            BaseAddress = new UriBuilder
            {
                Host = "localhost",
                Port = _containers[node].ToHostExposedEndpoint("8024/tcp").Port
            }.Uri
        };
    }

    public DnsEndPoint[] GetGrpcEndpoints()
    {
        if (_containers == null)
        {
            throw new InvalidOperationException($"[{_id}]The cluster nodes have not been initialized");
        }

        return Array.ConvertAll(
            _containers,
            clusterNode => new DnsEndPoint(
                "localhost",
                clusterNode.ToHostExposedEndpoint("8124/tcp").Port
            )
        );
    }

    public GrpcChannel CreateGrpcChannel(int node, GrpcChannelOptions? options)
    {
        if (_containers == null)
        {
            throw new InvalidOperationException($"[{_id}]The cluster nodes have not been initialized");
        }
        if (node < 0 || node >= _containers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(node), node,
                $"[{_id}]The node index is out of the acceptable range: [0,{_containers.Length}]");
        }
        
        var address = new UriBuilder
        {
            Host = "localhost",
            Port = _containers[node].ToHostExposedEndpoint("8124/tcp").Port
        }.Uri;
        return options == null ? GrpcChannel.ForAddress(address) : GrpcChannel.ForAddress(address, options);
    }

    public Task DisposeAsync()
    {
        _logger.LogDebug("[{Cluster}]Embedded Axon Cluster is being disposed", _id);
        
        if (_containers != null)
        {
            if (_network != null)
            {
                Array.ForEach(_containers, container =>
                {
                    _network.Detach(container);
                });
            }
            
            Array.ForEach(_containers, container =>
            {
                container.Remove(true);
                container.Dispose();
            });
        }

        _network?.Dispose();

        Array.ForEach(_nodes, node => node.Dispose());

        _logger.LogDebug("[{Cluster}]Embedded Axon Cluster got disposed", _id);
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
        var nodeProperties = new[] { new EmbeddedAxonClusterNode(node1), new EmbeddedAxonClusterNode(node2), new EmbeddedAxonClusterNode(node3) };
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
                            Context = Context.Default.ToString(),
                            Roles = new[] { "USE_CONTEXT" }
                        },
                        new()
                        {
                            Context = Context.Admin.ToString(),
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
                    Name = Context.Default.ToString(),
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = Context.Default.ToString()
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-1",
                            Role = "PRIMARY"
                        },
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
                    Name = Context.Admin.ToString(),
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = Context.Admin.ToString()
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-1",
                            Role = "PRIMARY"
                        },
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
                }
            }
        };
        return new EmbeddedAxonCluster(nodeProperties, template, logger);
    }

    public static IAxonCluster WithAccessControlEnabled(ILogger<EmbeddedAxonCluster> logger)
    {
        var cluster = new SystemProperties
        {
            ClusterSetup =
            {
                AutoclusterFirst = "axonserver-1",
                AutoclusterContexts = new[] { Context.Admin.ToString(), Context.Default.ToString() }
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
        var nodeProperties = new[] { new EmbeddedAxonClusterNode(node1), new EmbeddedAxonClusterNode(node2), new EmbeddedAxonClusterNode(node3) };
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
                            Context = Context.Default.ToString(),
                            Roles = new[] { "USE_CONTEXT" }
                        },
                        new()
                        {
                            Context = Context.Admin.ToString(),
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
                    Name = Context.Default.ToString(),
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = Context.Default.ToString()
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-1",
                            Role = "PRIMARY"
                        },
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
                    Name = Context.Admin.ToString(),
                    Contexts = new ClusterTemplateReplicationGroupContext[]
                    {
                        new()
                        {
                            Name = Context.Admin.ToString()
                        }
                    },
                    Roles = new ClusterTemplateReplicationGroupRole[]
                    {
                        new()
                        {
                            Node = "axonserver-1",
                            Role = "PRIMARY"
                        },
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
                }
            }
        };
        return new EmbeddedAxonCluster(nodeProperties, template, logger);
    }
}