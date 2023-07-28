using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServerIntegrationTests;

public class AxonServerGrpcChannelFactoryTests
{
    public class WhenServerIsNotReachable
    {
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenServerIsNotReachable(ITestOutputHelper output)
        {
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new TestOutputHelperLoggerFactory(output);
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var clock = () => DateTimeOffset.UtcNow;
            var connectionTimeout = AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout;
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();

            var sut = new AxonServerGrpcChannelFactory(
                clientIdentity, 
                AxonServerAuthentication.None,
                routingServers, 
                _loggerFactory, 
                Array.Empty<Interceptor>(),
                new GrpcChannelOptions(),
                clock,
                connectionTimeout);

            var result = await sut.Create(context);

            Assert.Null(result);
        }
    }

    [Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
    public class WhenServerHasAccessControlDisabled
    {
        private readonly IAxonServer _server;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenServerHasAccessControlDisabled(AxonServerWithAccessControlDisabled server, ITestOutputHelper output)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
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
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = CreateSystemUnderTest(routingServers);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
        
        [Fact]
        public async Task CreateReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
    }

    [Collection(nameof(AxonServerWithAccessControlEnabledCollection))]
    public class WhenServerHasAccessControlEnabled
    {
        private readonly IAxonServer _server;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenServerHasAccessControlEnabled(AxonServerWithAccessControlEnabled server, ITestOutputHelper output)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
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
                new GrpcChannelOptions(), clock, AxonServerConnectionDefaults.DefaultReconnectOptions.ConnectionTimeout);
        }
        
        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResult()
        {
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = CreateSystemUnderTest(routingServers);

            var result = await sut.Create(context);

            Assert.Null(result);
        }
        
        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers, AxonServerAuthentication.None);

            var result = await sut.Create(context);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResult()
        {
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = CreateSystemUnderTest(
                routingServers,
                AxonServerAuthentication.UsingToken(_server.Properties.AccessControl.AccessControlToken!));

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
        
        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = CreateSystemUnderTest(routingServers,
                AxonServerAuthentication.UsingToken(_server.Properties.AccessControl.AccessControlToken!));

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result.Target);
        }
    }
}