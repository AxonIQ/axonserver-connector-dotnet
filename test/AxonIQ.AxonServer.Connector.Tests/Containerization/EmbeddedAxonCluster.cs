using System.Net;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class EmbeddedAxonCluster : IAxonCluster
{
    private readonly int _id = EmbeddedAxonClusterCounter.Next();
    
    private readonly EmbeddedAxonClusterNode[] _nodes;
    private readonly ILogger<EmbeddedAxonCluster> _logger;

    private Context[]? _contexts;
    private INetworkService? _network;

    private EmbeddedAxonCluster(EmbeddedAxonClusterNode[] nodes, ILogger<EmbeddedAxonCluster> logger)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
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

    public async Task InitializeAsync()
    {
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster is being initialized", _id);

        _network = new Builder().UseNetwork($"axoniq-dotnet-{_id}").ReuseIfExist().Build();

        foreach (var node in _nodes)
        {
            node.Start(_network);
        }

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got started", _id);
        
        foreach (var node in _nodes)
        {
            await node.WaitUntilAvailableAsync(_id);
        }

        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster became available", _id);
        _logger.LogDebug("[{ClusterId}]Embedded Axon Cluster got initialized", _id);
    }
    
    public DnsEndPoint[] GetHttpEndpoints()
    {
        return Array.ConvertAll(_nodes, node => node.GetHttpEndpoint());
    }

    public DnsEndPoint[] GetGrpcEndpoints()
    {
        return Array.ConvertAll(_nodes, node => node.GetGrpcEndpoint());
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
        var nodes = new[]
        {
            new EmbeddedAxonClusterNode(node1, template, logger), 
            new EmbeddedAxonClusterNode(node2, template, logger), 
            new EmbeddedAxonClusterNode(node3, template, logger)
        };
        return new EmbeddedAxonCluster(nodes, logger);
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
        var nodes = new[]
        {
            new EmbeddedAxonClusterNode(node1, template, logger), 
            new EmbeddedAxonClusterNode(node2, template, logger), 
            new EmbeddedAxonClusterNode(node3, template, logger)
        };
        return new EmbeddedAxonCluster(nodes, logger);
    }
}