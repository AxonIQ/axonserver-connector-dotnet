using System.Net;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnectionFactoryOptionsBuilder
{
    /// <summary>
    /// Specifies the component name this factory identifies as to the server.
    /// </summary>
    /// <param name="name">The component name</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder AsComponentName(ComponentName name);
    /// <summary>
    /// Specifies the client instance identifier this factory identifies as to the server.
    /// </summary>
    /// <param name="id">The client instance identifier</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder AsClientInstanceId(ClientInstanceId id);
    /// <summary>
    /// Indicates the factory should use the default server addresses to connect to the server.
    /// </summary>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithDefaultRoutingServers();
    /// <summary>
    /// Specifies the server addresses the factory should use to connect to the server.
    /// </summary>
    /// <param name="servers">One or more server addresses to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(params DnsEndPoint[] servers);
    /// <summary>
    /// Specifies the server addresses the factory should use to connect to the server.
    /// </summary>
    /// <param name="servers">One or more server addresses to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(IEnumerable<DnsEndPoint> servers);
    /// <summary>
    /// Indicates the factory should not authenticate to the server (e.g. if the server has disabled authentication).
    /// </summary>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithoutAuthentication();
    /// <summary>
    /// Indicates the factory should authenticate to the server using the specified <c>token</c>.
    /// </summary>
    /// <param name="token">The token used to authenticate</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithAuthenticationToken(string token);
    /// <summary>
    /// Specifies a client tag the factory should use.
    /// </summary>
    /// <param name="key">The key of the tag</param>
    /// <param name="value">The value of the tag</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithClientTag(string key, string value);
    /// <summary>
    /// Specifies one or more clients tag the factory should use.
    /// </summary>
    /// <param name="tags">The tags to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(params KeyValuePair<string, string>[] tags);
    /// <summary>
    /// Specifies one or more clients tag the factory should use.
    /// </summary>
    /// <param name="tags">The tags to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(IEnumerable<KeyValuePair<string, string>> tags);
    /// <summary>
    /// Specifies the logger factory the factory should use.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    /// <remarks>This method must not be called if using any of the <see cref="AddAxonServerConnectionFactory()"/> methods on the <see cref="ServiceCollection"/>.</remarks>
    IAxonServerConnectionFactoryOptionsBuilder WithLoggerFactory(ILoggerFactory loggerFactory);
    /// <summary>
    /// Specifies the clock the factory should use.
    /// </summary>
    /// <param name="clock">The clock to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    /// <remarks>This method exists for testing purposes only.</remarks>
    IAxonServerConnectionFactoryOptionsBuilder WithClock(Func<DateTimeOffset> clock);
    /// <summary>
    /// Specifies the gRPC channel options the factory should use.
    /// </summary>
    /// <param name="grpcChannelOptions">The gRPC channel options to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    /// <remarks>The factory will override certain options if it relies on their functionality.</remarks>
    IAxonServerConnectionFactoryOptionsBuilder WithGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions);
    /// <summary>
    /// Specifies the gRPC interceptors the factory should use.
    /// </summary>
    /// <param name="interceptors">The gRPC interceptors to use</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    /// <remarks>This method exists for testing purposes only.</remarks>
    IAxonServerConnectionFactoryOptionsBuilder WithInterceptors(params Interceptor[] interceptors);
    /// <summary>
    /// Specifies the number of commands each connection of the factory can process.
    /// </summary>
    /// <param name="count">The number of commands to process</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithCommandPermits(PermitCount count);
    /// <summary>
    /// Specifies the number of queries each connection of the factory can process.
    /// </summary>
    /// <param name="count">The number of queries to process</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithQueryPermits(PermitCount count);
    /// <summary>
    /// Specifies the reconnect options each connection of the factory should use.
    /// </summary>
    /// <param name="options">The reconnect options</param>
    /// <returns>An instance of the builder to continue configuring options with.</returns>
    IAxonServerConnectionFactoryOptionsBuilder WithReconnectOptions(ReconnectOptions options);
    /// <summary>
    /// Builds the configured options and falls back to defaults for those options that have not been specified.
    /// </summary>
    /// <returns>An instance of the configured options.</returns>
    AxonServerConnectionFactoryOptions Build();
}