using System.Net;
using AutoFixture;
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerGrpcChannelFactoryTests
{
    public class WhenRoutingServersAreNotReachable
    {
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenRoutingServersAreNotReachable()
        {
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = _fixture.CreateMany<DnsEndPoint>(Random.Shared.Next(1, 5)).ToArray();

            var sut = new AxonServerGrpcChannelFactory(clientIdentity, context, AxonServerAuthentication.None,
                routingServers, _loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>());

            var result = await sut.Create();

            Assert.Null(result);
        }
    }

    [Collection(nameof(AxonServerContainerWithAccessControlDisabledCollection))]
    public class WhenRoutingServersWithAccessControlDisabledAreReachable
    {
        private readonly AxonServerContainerWithAccessControlDisabled _server;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenRoutingServersWithAccessControlDisabledAreReachable(
            AxonServerContainerWithAccessControlDisabled server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, context, AxonServerAuthentication.None,
                routingServers, _loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>());

            var result = await sut.Create();

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
    }

    [Collection(nameof(AxonServerContainerWithAccessControlEnabledCollection))]
    public class WhenRoutingServersWithAccessControlEnabledAreReachable
    {
        private readonly AxonServerContainerWithAccessControlEnabled _server;
        private readonly Fixture _fixture;
        private readonly ILoggerFactory _loggerFactory;

        public WhenRoutingServersWithAccessControlEnabledAreReachable(
            AxonServerContainerWithAccessControlEnabled server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _fixture = new Fixture();
            _fixture.CustomizeComponentName();
            _fixture.CustomizeClientInstanceId();
            _fixture.CustomizeContext();
            _loggerFactory = new NullLoggerFactory();
        }

        [Fact]
        public async Task CreateWithoutAuthenticationReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, context, AxonServerAuthentication.None,
                routingServers, _loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>());

            var result = await sut.Create();

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateWithAuthenticationTokenReturnsExpectedResult()
        {
            var clientIdentity = _fixture.Create<ClientIdentity>();
            var context = _fixture.Create<Context>();
            var routingServers = new[] { _server.GetGrpcEndpoint() };
            var sut = new AxonServerGrpcChannelFactory(clientIdentity, context,
                AxonServerAuthentication.UsingToken(_server.Token), routingServers,
                _loggerFactory.CreateLogger<AxonServerGrpcChannelFactory>());

            var result = await sut.Create();

            Assert.NotNull(result);
            Assert.Equal(_server.GetGrpcEndpoint().ToUri().Authority, result!.Target);
        }
    }
}