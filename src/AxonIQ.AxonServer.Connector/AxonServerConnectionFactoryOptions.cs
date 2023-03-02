using System.Net;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerConnectionFactoryOptions
{
    public static IAxonServerConnectionFactoryOptionsBuilder For(ComponentName componentName)
    {
        return new Builder(componentName, ClientInstanceId.GenerateFrom(componentName));
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
                ? ClientInstanceId.GenerateFrom(componentName)
                : new ClientInstanceId(configuration[AxonServerConnectionFactoryConfiguration.ClientInstanceId]);

        return new Builder(componentName, clientInstanceId);
    }

    private AxonServerConnectionFactoryOptions(
        ComponentName componentName,
        ClientInstanceId clientInstanceId,
        IReadOnlyList<DnsEndPoint> routingServers,
        IReadOnlyDictionary<string, string> clientTags,
        IAxonServerAuthentication authentication,
        ILoggerFactory loggerFactory,
        Func<DateTimeOffset>? clock,
        GrpcChannelOptions? grpcChannelOptions,
        IReadOnlyList<Interceptor> interceptors,
        PermitCount commandPermits,
        PermitCount queryPermits,
        TimeSpan eventProcessorUpdateFrequency,
        BackoffPolicyOptions connectBackoffPolicyOptions)
    {
        ComponentName = componentName;
        ClientInstanceId = clientInstanceId;
        RoutingServers = routingServers;
        ClientTags = clientTags;
        Authentication = authentication;
        LoggerFactory = loggerFactory;
        Clock = clock ?? (() => DateTimeOffset.UtcNow);
        GrpcChannelOptions = grpcChannelOptions;
        Interceptors = interceptors;
        CommandPermits = commandPermits;
        QueryPermits = queryPermits;
        EventProcessorUpdateFrequency = eventProcessorUpdateFrequency;
        ConnectBackoffPolicyOptions = connectBackoffPolicyOptions;
    }

    public ComponentName ComponentName { get; }
    public ClientInstanceId ClientInstanceId { get; }
    public IReadOnlyList<DnsEndPoint> RoutingServers { get; }
    public IReadOnlyDictionary<string, string> ClientTags { get; }
    public IAxonServerAuthentication Authentication { get; }
    public ILoggerFactory LoggerFactory { get; }
    public Func<DateTimeOffset> Clock { get; }
    public GrpcChannelOptions? GrpcChannelOptions { get; }
    public IReadOnlyList<Interceptor> Interceptors { get; }
    public PermitCount CommandPermits { get; }
    public PermitCount QueryPermits { get; }
    public TimeSpan EventProcessorUpdateFrequency { get; }
    public BackoffPolicyOptions ConnectBackoffPolicyOptions { get; }

    //TODO: Extend this with more options as we go - we'll need to port all of the Java ones that make sense in .NET.

    private class Builder : IAxonServerConnectionFactoryOptionsBuilder
    {
        private ComponentName _componentName;
        private ClientInstanceId _clientInstanceId;
        private readonly List<DnsEndPoint> _routingServers;
        private readonly Dictionary<string, string> _clientTags;
        private IAxonServerAuthentication _authentication;
        private ILoggerFactory _loggerFactory;
        private Func<DateTimeOffset>? _clock;
        private GrpcChannelOptions? _grpcChannelOptions;
        private readonly List<Interceptor> _interceptors;
        private PermitCount _commandPermits;
        private PermitCount _queryPermits;
        private TimeSpan _eventProcessorUpdateFrequency;
        private BackoffPolicyOptions _connectBackoffPolicyOptions;

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
            _loggerFactory = new NullLoggerFactory();
            _clock = null;
            _grpcChannelOptions = null;
            _interceptors = new List<Interceptor>();
            _commandPermits = AxonServerConnectionFactoryDefaults.DefaultCommandPermits;
            _queryPermits = AxonServerConnectionFactoryDefaults.DefaultQueryPermits;
            _eventProcessorUpdateFrequency = AxonServerConnectionFactoryDefaults.DefaultEventProcessorUpdateFrequency;
            _connectBackoffPolicyOptions = AxonServerConnectionFactoryDefaults.DefaultConnectBackoffPolicyOptions;
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

        public IAxonServerConnectionFactoryOptionsBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _loggerFactory = loggerFactory;
            
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithClock(Func<DateTimeOffset> clock)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            _clock = clock;
            
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
        {
            if (grpcChannelOptions == null) throw new ArgumentNullException(nameof(grpcChannelOptions));

            _grpcChannelOptions = grpcChannelOptions;

            return this;
        }
        
        public IAxonServerConnectionFactoryOptionsBuilder WithInterceptors(params Interceptor[] interceptors)
        {
            if (interceptors == null) throw new ArgumentNullException(nameof(interceptors));

            _interceptors.AddRange(interceptors);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithCommandPermits(PermitCount count)
        {
            _commandPermits = PermitCount.Max(AxonServerConnectionFactoryDefaults.MinimumCommandPermits, count);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithQueryPermits(PermitCount count)
        {
            _queryPermits = PermitCount.Max(AxonServerConnectionFactoryDefaults.MinimumQueryPermits, count);

            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithConnectBackoffPolicy(BackoffPolicyOptions options)
        {
            _connectBackoffPolicyOptions = options ?? throw new ArgumentNullException(nameof(options));
            
            return this;
        }

        public IAxonServerConnectionFactoryOptionsBuilder WithEventProcessorUpdateFrequency(TimeSpan frequency)
        {
            _eventProcessorUpdateFrequency = TimeSpanMath.Max(AxonServerConnectionFactoryDefaults.DefaultEventProcessorUpdateFrequency, frequency);

            return this;
        }

        public AxonServerConnectionFactoryOptions Build()
        {
            if (_routingServers.Count == 0)
            {
                _routingServers.AddRange(AxonServerConnectionFactoryDefaults.RoutingServers);
            }

            return new AxonServerConnectionFactoryOptions(
                _componentName, 
                _clientInstanceId, 
                _routingServers,
                _clientTags, 
                _authentication, 
                _loggerFactory,
                _clock,
                _grpcChannelOptions,
                _interceptors,
                _commandPermits,
                _queryPermits,
                _eventProcessorUpdateFrequency,
                _connectBackoffPolicyOptions);
        }
    }
}