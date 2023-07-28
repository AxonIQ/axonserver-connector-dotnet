using System.Net;
using AutoFixture;
using AxonIQ.AxonClusterIntegrationTests.Containerization;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonClusterIntegrationTests;

public class AxonServerGrpcChannelFactoryTests
{
    [Collection(nameof(AxonClusterWithAccessControlDisabledCollection))]
    public class WhenClusterHasAccessControlDisabled
    {
        private readonly IAxonCluster _cluster;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenClusterHasAccessControlDisabled(
            AxonClusterWithAccessControlDisabled cluster, 
            ITestOutputHelper output)
        {
            _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new TestOutputHelperLoggerFactory(output);
        }
        
        private AxonServerGrpcChannelFactory CreateSystemUnderTest(IReadOnlyList<DnsEndPoint> routingServers)
        {
            var clock = () => DateTimeOffset.UtcNow;
            var clientIdentity = _fixture.Create<ClientIdentity>();
            return new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory, Array.Empty<Interceptor>(), new GrpcChannelOptions(),
                clock, AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout);
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var context = Context.Default;
            var routingServers = _cluster.GetGrpcEndpoints();
            var sut =  CreateSystemUnderTest(routingServers);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(routingServers[0].ToUri().Authority, result.Target);
        }
        
        [Fact]
        public async Task CreateReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = Context.Default;
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _cluster.Nodes[0].GetGrpcEndpoint());

            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers);
        
            var result = await sut.Create(context);
        
            Assert.NotNull(result);
            Assert.Equal(_cluster.Nodes[0].GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
    }
    
    [Collection(nameof(AxonClusterWithAccessControlEnabledCollection))]
    public class WhenClusterHasAccessControlEnabled
    {
        private readonly IAxonCluster _cluster;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenClusterHasAccessControlEnabled(
            AxonClusterWithAccessControlEnabled cluster, 
            ITestOutputHelper output)
        {
            _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new TestOutputHelperLoggerFactory(output);
        }
        
        private AxonServerGrpcChannelFactory CreateSystemUnderTest(IReadOnlyList<DnsEndPoint> routingServers)
        {
            var clock = () => DateTimeOffset.UtcNow;
            var clientIdentity = _fixture.Create<ClientIdentity>();
            return new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory, Array.Empty<Interceptor>(), new GrpcChannelOptions(),
                clock, AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout);
        }
        
        private AxonServerGrpcChannelFactory CreateSystemUnderTest(IReadOnlyList<DnsEndPoint> routingServers, IAxonServerAuthentication authentication)
        {
            var clock = () => DateTimeOffset.UtcNow;
            var clientIdentity = _fixture.Create<ClientIdentity>();
            return new AxonServerGrpcChannelFactory(clientIdentity, authentication, routingServers, _loggerFactory, Array.Empty<Interceptor>(),
                new GrpcChannelOptions(),
                clock, AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout);
        }

        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResult()
        {
            var context = Context.Default;
            var routingServers = _cluster.GetGrpcEndpoints();
            var sut = CreateSystemUnderTest(routingServers);

            var result = await sut.Create(context);

            Assert.Null(result);
        }
        
        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = Context.Default;
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _cluster.Nodes[0].GetGrpcEndpoint());

            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers, AxonServerAuthentication.None);
        
            var result = await sut.Create(context);
        
            Assert.Null(result);
        }
        
        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResult()
        {
            var context = Context.Default;
            var routingServers = _cluster.GetGrpcEndpoints();
            var sut = CreateSystemUnderTest(routingServers, AxonServerAuthentication.UsingToken(_cluster.Nodes[0].Template.Applications![0].Token!));

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_cluster.Nodes[0].GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
        
        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = Context.Default;
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _cluster.Nodes[0].GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers, AxonServerAuthentication.UsingToken(_cluster.Nodes[0].Template.Applications![0].Token!));

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_cluster.Nodes[0].GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
    }

    [Collection(nameof(SelfServiceAxonClusterCollection))]
    public class WhenClusterHasDedicatedReplicationGroupsOnDedicatedNodesForContext
    {
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;
 
        public WhenClusterHasDedicatedReplicationGroupsOnDedicatedNodesForContext(
            ITestOutputHelper output)
        {
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new TestOutputHelperLoggerFactory(output);
        }

        [Fact(Skip = "On CI, causes a PermissionDenied RpcException because supposedly the token is invalid for the PlatformService/GetPlatform")]
        public async Task CreateReturnsExpectedResult()
        {
            var common = new SystemProperties
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
                }
            };
            var node1 = common.Clone();
            node1.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
            node1.NodeSetup.Hostname = "localhost";
            node1.NodeSetup.Port = 9124;
            node1.NodeSetup.InternalHostname = node1.NodeSetup.Name;
            var node2 = common.Clone();
            node2.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
            node2.NodeSetup.Hostname = "localhost";
            node2.NodeSetup.Port = 9324;
            node2.NodeSetup.InternalHostname = node2.NodeSetup.Name;
            var node3 = common.Clone();
            node3.NodeSetup.Name = $"axonserver-{AxonServerCounter.Next()}";
            node3.NodeSetup.Hostname = "localhost";
            node3.NodeSetup.Port = 9624;
            node3.NodeSetup.InternalHostname = node3.NodeSetup.Name;
            var template = new ClusterTemplate
            {
                First = $"{node1.NodeSetup.InternalHostname ?? "localhost"}:{node1.NodeSetup.InternalPort ?? 8224}",
                Users = new ClusterTemplateUser[]
                {
                  new()
                  {
                      UserName = "dotnet",
                      Password = "p@ssw0rd",
                      Roles = new ClusterTemplateUserRole[]
                      {
                          new()
                          {
                              Context = Context.Admin.ToString(),
                              Roles = new[] {"ADMIN"}
                          },
                          new()
                          {
                              Context = Context.Default.ToString(),
                              Roles = new[] {"ADMIN"}
                          }
                      }
                  }  
                },
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
                new EmbeddedAxonClusterNode(node1, template, _loggerFactory.CreateLogger<EmbeddedAxonCluster>()),
                new EmbeddedAxonClusterNode(node2, template, _loggerFactory.CreateLogger<EmbeddedAxonCluster>()),
                new EmbeddedAxonClusterNode(node3, template, _loggerFactory.CreateLogger<EmbeddedAxonCluster>())
            };
            var cluster = new EmbeddedAxonCluster(nodes, _loggerFactory.CreateLogger<EmbeddedAxonCluster>());
            try
            {
                await cluster.InitializeAsync();
                await cluster.WaitUntilAvailableAsync();
                
                var clock = () => DateTimeOffset.UtcNow;
                var clientIdentity = _fixture.Create<ClientIdentity>();
                var context = Context.Default;
                var routingServers = cluster.GetGrpcEndpoints();
                var sut = new AxonServerGrpcChannelFactory(clientIdentity,
                    AxonServerAuthentication.UsingToken(template.Applications![0].Token!),
                    routingServers, _loggerFactory, Array.Empty<Interceptor>(), new GrpcChannelOptions(),
                    clock, AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout);

                var result = await sut.Create(context);

                Assert.NotNull(result);
                Assert.Contains(result.Target, new[]
                {
                    cluster.Nodes[1].GetGrpcEndpoint().ToUri().Authority,
                    cluster.Nodes[2].GetGrpcEndpoint().ToUri().Authority
                });
            }
            finally
            {
                await cluster.DisposeAsync();
            }
        }
    }
}