using System.Net;
using Microsoft.Extensions.Configuration;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactoryOptions
{
    public static IAxonServerConnectionFactoryOptionsBuilder For(ComponentName componentName)
    {
        return new Builder(componentName, Connector.ClientInstanceId.GenerateFrom(componentName));
    }

    public static IAxonServerConnectionFactoryOptionsBuilder For(ComponentName componentName,
        ClientInstanceId clientInstanceId)
    {
        return new Builder(componentName, clientInstanceId);
    }

    public static IAxonServerConnectionFactoryOptionsBuilder FromConfiguration(IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        var componentName =
            configuration[AxonServerConnectionFactoryConfiguration.ComponentName] == null
                ? ComponentName.GenerateRandomName()
                : new ComponentName(configuration[AxonServerConnectionFactoryConfiguration.ComponentName]);
        var clientInstanceId =
            configuration[AxonServerConnectionFactoryConfiguration.ClientInstanceId] == null
                ? Connector.ClientInstanceId.GenerateFrom(componentName)
                : new ClientInstanceId(configuration[AxonServerConnectionFactoryConfiguration.ClientInstanceId]);
        var builder = new Builder(componentName, clientInstanceId);

        return builder;
    }

    private AxonServerConnectionFactoryOptions(
        ComponentName componentName,
        ClientInstanceId clientInstanceId,
        IReadOnlyCollection<DnsEndPoint> routingServers,
        IReadOnlyDictionary<string, string> clientTags,
        IAxonServerAuthentication authentication)
    {
        ComponentName = componentName;
        ClientInstanceId = clientInstanceId;
        RoutingServers = routingServers;
        ClientTags = clientTags;
        Authentication = authentication;
    }

    public ComponentName ComponentName { get; }
    public ClientInstanceId ClientInstanceId { get; }
    public IReadOnlyCollection<DnsEndPoint> RoutingServers { get; }
    public IReadOnlyDictionary<string, string> ClientTags { get; }
    public IAxonServerAuthentication Authentication { get; }

    //TODO: Extend this with more options as we go - we'll need to port all of the Java ones that make sense in .NET.

    private class Builder : IAxonServerConnectionFactoryOptionsBuilder
    {
        private ComponentName _componentName;
        private ClientInstanceId _clientInstanceId;
        private readonly List<DnsEndPoint> _routingServers;
        private readonly Dictionary<string, string> _clientTags;
        private IAxonServerAuthentication _authentication;

        internal Builder(ComponentName componentName, ClientInstanceId clientInstanceId)
        {
            _componentName = componentName;
            _clientInstanceId = clientInstanceId;
            _routingServers = new List<DnsEndPoint>(AxonServerConnectionFactoryDefaults.RoutingServers);
            _clientTags = new Dictionary<string, string>();
            foreach (var (key, value) in AxonServerConnectionFactoryDefaults.ClientTags)
            {
                _clientTags.Add(key, value);
            }

            _authentication = AxonServerAuthentication.None;
        }

        public IAxonServerConnectionFactoryOptionsBuilder AsComponentName(ComponentName name)
        {
            _componentName = name;
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder AsClientInstanceId(ClientInstanceId id)
        {
            _clientInstanceId = id;
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithDefaultRoutingServers()
        {
            _routingServers.Clear();
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(params DnsEndPoint[] servers)
        {
            if (servers == null) throw new ArgumentNullException(nameof(servers));

            _routingServers.Clear();
            _routingServers.AddRange(servers);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(IEnumerable<DnsEndPoint> servers)
        {
            if (servers == null) throw new ArgumentNullException(nameof(servers));

            _routingServers.Clear();
            _routingServers.AddRange(servers);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithoutAuthentication()
        {
            _authentication = AxonServerAuthentication.None;

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithAuthenticationToken(string token)
        {
            _authentication = AxonServerAuthentication.UsingToken(token);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithClientTag(string key, string value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            _clientTags[key] = value;

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithClientTags(params KeyValuePair<string, string>[] tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));

            foreach (var (key, value) in tags)
            {
                _clientTags[key] = value;
            }

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithClientTags(IEnumerable<KeyValuePair<string, string>> tags)
        {
            if (tags == null) throw new ArgumentNullException(nameof(tags));

            foreach (var (key, value) in tags)
            {
                _clientTags[key] = value;
            }

            return this;
        }

        public AxonServerConnectionFactoryOptions Build()
        {
            if (_routingServers.Count == 0)
            {
                _routingServers.AddRange(AxonServerConnectionFactoryDefaults.RoutingServers);
            }

            return new AxonServerConnectionFactoryOptions(_componentName, _clientInstanceId, _routingServers,
                _clientTags, _authentication);
        }
    }
}