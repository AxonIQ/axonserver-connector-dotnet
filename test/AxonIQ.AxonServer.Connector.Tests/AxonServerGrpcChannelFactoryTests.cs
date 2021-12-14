using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerGrpcChannelFactoryTests
{
    public class WhenServerIsNotReachable
    {
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenServerIsNotReachable()
        {
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();

            var sut = new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory);

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

        public WhenServerHasAccessControlDisabled(
            AxonServerWithAccessControlDisabled server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
        
        [Fact]
        public async Task CreateReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
    }

    [Collection(nameof(AxonServerWithAccessControlEnabledCollection))]
    public class WhenServerHasAccessControlEnabled
    {
        private readonly IAxonServer _server;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenServerHasAccessControlEnabled(
            AxonServerWithAccessControlEnabled server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _fixture.CustomizeLocalHostDnsEndPointInReservedPortRange();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, AxonServerAuthentication.None,
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.Null(result);
        }
        
        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, 
                AxonServerAuthentication.None,
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity,
                AxonServerAuthentication.UsingToken(_server.Properties.AccessControl.AccessControlToken!),
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
        
        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResultWhenAtLeastOneRoutingServerIsReachable()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var servers = new List<DnsEndPoint>(
                _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5))
            );
            servers.Insert(Random.Shared.Next(0, servers.Count), _server.GetGrpcEndpoint());
            var routingServers = servers.ToArray();
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, 
                AxonServerAuthentication.UsingToken(_server.Properties.AccessControl.AccessControlToken!),
                routingServers, _loggerFactory);

            var result = await sut.Create(context);

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
    }
}