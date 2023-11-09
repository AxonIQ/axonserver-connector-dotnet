using System.Net;
using AxonIQ.AxonServer.Connector;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Embedded;

public class EmbeddedAxonCluster : IAxonCluster
{
    private readonly string _id = AxonClusterCounter.Next();
    
    private readonly EmbeddedAxonClusterNode[] _nodes;
    private readonly ILogger<EmbeddedAxonCluster> _logger;

    private Context[]? _contexts;
    private INetworkService? _network;

    public EmbeddedAxonCluster(EmbeddedAxonClusterNode[] nodes, ILogger<EmbeddedAxonCluster> logger)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<IAxonClusterNode> Nodes => _nodes
        .Select<EmbeddedAxonClusterNode, IAxonClusterNode>(node => node)
        .ToArray();

    public IReadOnlyList<Context> Contexts
    {
        get
        {
            if (_contexts == null)
            {
                var contexts = new HashSet<Context>();

                foreach (var node in _nodes)
                {
                    foreach (var context in node.ScanForContexts())
                    {
                        contexts.Add(context);
                    }
                }

                _contexts = contexts.ToArray();
            }

            return _contexts;
        }
    }

    public Task InitializeAsync()
    {
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster is being initialized", _id);

        _network = new Builder().UseNetwork($"axon-cluster-network-{_id}").Build();

        foreach (var node in _nodes)
        {
            node.Start(_network);
        }

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got started", _id);
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got initialized", _id);
        
        return Task.CompletedTask;
    }

    public async Task WaitUntilAvailableAsync(TimeSpan? maximumWaitTime = default, TimeSpan? delayBetweenAttempts = default)
    {
        foreach (var node in _nodes)
        {
            await node.WaitUntilAvailableAsync(_id, maximumWaitTime, delayBetweenAttempts);
        }

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster became available", _id);
    }
    
    public IReadOnlyList<DnsEndPoint> GetHttpEndpoints()
    {
        return Array.ConvertAll(_nodes, node => node.GetHttpEndpoint());
    }
    
    public DnsEndPoint GetRandomHttpEndpoint()
    {
        var endpoints = GetHttpEndpoints();
        return endpoints[Random.Shared.Next(0, endpoints.Count)];
    }

    public IReadOnlyList<DnsEndPoint> GetGrpcEndpoints()
    {
        return Array.ConvertAll(_nodes, node => node.GetGrpcEndpoint());
    }
    
    public DnsEndPoint GetRandomGrpcEndpoint()
    {
        var endpoints = GetGrpcEndpoints();
        return endpoints[Random.Shared.Next(0, endpoints.Count)];
    }

    public Task DisposeAsync()
    {
        _logger.LogDebug("[{Cluster}]Embedded Axon Cluster is being disposed", _id);

        foreach (var node in _nodes)
        {
            node.Stop(_network);
        }

        _network?.Dispose();

        _logger.LogDebug("[{Cluster}]Embedded Axon Cluster got disposed", _id);
        return Task.CompletedTask;
    }

    public static IAxonCluster
        WithAccessControlDisabled(ILogger<EmbeddedAxonCluster> logger, bool emitNodeLogs = false)
    {
        var cluster = new SystemProperties
        {
            ClusterSetup =
            {
                ClusterTemplatePath = "/axonserver/config/cluster-template.yml"
            },
            AccessControl =
            {
                AccessControlEnabled = false,
                AccessControlInternalToken = Guid.NewGuid().ToString("N")
            },
            KeepAlive =
            {
                HeartbeatEnabled = true
            }
        }; 
        var node1 = cluster.Clone();
        node1.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node1.NodeSetup.Hostname = "localhost";
        node1.NodeSetup.InternalHostname = node1.NodeSetup.Name;
        var node2 = cluster.Clone();
        node2.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node2.NodeSetup.Hostname = "localhost";
        node2.NodeSetup.InternalHostname = node2.NodeSetup.Name;
        var node3 = cluster.Clone();
        node3.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node3.NodeSetup.Hostname = "localhost";
        node3.NodeSetup.InternalHostname = node3.NodeSetup.Name;
        var template = new ClusterTemplate
        {
            First = node1.NodeSetup.Name,
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
                            Node = node1.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node2.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node3.NodeSetup.Name,
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
                            Node = node1.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node2.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node3.NodeSetup.Name,
                            Role = "PRIMARY"
                        }
                    }
                }
            }
        };
        var nodes = new[]
        {
            new EmbeddedAxonClusterNode(node1, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }, 
            new EmbeddedAxonClusterNode(node2, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }, 
            new EmbeddedAxonClusterNode(node3, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }
        };
        return new EmbeddedAxonCluster(nodes, logger);
    }

    public static IAxonCluster WithAccessControlEnabled(ILogger<EmbeddedAxonCluster> logger, bool emitNodeLogs = false)
    {
        var cluster = new SystemProperties
        {
            ClusterSetup =
            {
                ClusterTemplatePath = "/axonserver/config/cluster-template.yml"
            },
            AccessControl =
            {
                AccessControlEnabled = true,
                AccessControlInternalToken = Guid.NewGuid().ToString("N"),
                AccessControlSystemToken = Guid.NewGuid().ToString("N")
            },
            KeepAlive =
            {
                HeartbeatEnabled = true
            }
        };
        var node1 = cluster.Clone();
        node1.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node1.NodeSetup.Hostname = "localhost";
        node1.NodeSetup.InternalHostname = node1.NodeSetup.Name;
        var node2 = cluster.Clone();
        node2.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node2.NodeSetup.Hostname = "localhost";
        node2.NodeSetup.InternalHostname = node2.NodeSetup.Name;
        var node3 = cluster.Clone();
        node3.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
        node3.NodeSetup.Hostname = "localhost";
        node3.NodeSetup.InternalHostname = node3.NodeSetup.Name;
        var template = new ClusterTemplate
        {
            First = node1.NodeSetup.Name,
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
                            Node = node1.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node2.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node3.NodeSetup.Name,
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
                            Node = node1.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node2.NodeSetup.Name,
                            Role = "PRIMARY"
                        },
                        new()
                        {
                            Node = node3.NodeSetup.Name,
                            Role = "PRIMARY"
                        }
                    }
                }
            }
        };
        var nodes = new[]
        {
            new EmbeddedAxonClusterNode(node1, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }, 
            new EmbeddedAxonClusterNode(node2, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }, 
            new EmbeddedAxonClusterNode(node3, template, logger)
            {
                EmitNodeLogsOnStop = emitNodeLogs
            }
        };
        return new EmbeddedAxonCluster(nodes, logger);
    }
}