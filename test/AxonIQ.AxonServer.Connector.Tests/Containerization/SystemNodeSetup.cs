namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class SystemNodeSetup
{
    /// <summary>
    /// Unique node name of the Axon Server node. Default value is hostname of the server.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Hostname of the Axon Server node as communicated to clients. Default value is hostname of the server.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Hostname as communicated to other nodes of the cluster. Default value is hostname of the server.
    /// </summary>
    public string? InternalHostname { get; set; }

    /// <summary>
    /// Domain of this node as communicated to clients. Optional, if set will be appended to the hostname in communication with clients. Default value is none.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Domain as communicated to other nodes of the cluster. Default value is domain.
    /// </summary>
    public string? InternalDomain { get; set; }

    /// <summary>
    /// gRPC port for the Axon Server node. Default values is 8124.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// HTTP port for the Axon Server console. Default values is 8024.
    /// </summary>
    public int? ServerPort { get; set; }

    /// <summary>
    /// gRPC port for communication between Axon Server nodes within a cluster (Axon EE only). Default values is 8224.
    /// </summary>
    public int? InternalPort { get; set; }

    /// <summary>
    /// Key/value pairs of tags for tag based connections for clients. Default value is none.
    /// </summary>
    public KeyValuePair<string, string>[]? Tags { get; set; }

    /// <summary>
    /// Development mode which allows deleting all events from event store (Axon SE only). Default value is false.
    /// </summary>
    public bool? DevModeEnabled { get; set; }

    /// <summary>
    /// Set WebSocket CORS Allowed Origins. Default value is false.
    /// </summary>
    public bool? SetWebSocketAllowedOrigins { get; set; }

    public string[] Serialize()
    {
        var properties = new List<string>();
        if (!string.IsNullOrEmpty(Name))
        {
            properties.Add($"axoniq.axonserver.name={Name}");
        }
        if (!string.IsNullOrEmpty(Hostname))
        {
            properties.Add($"axoniq.axonserver.hostname={Hostname}");
        }
        if (!string.IsNullOrEmpty(InternalHostname))
        {
            properties.Add($"axoniq.axonserver.internal-hostname={InternalHostname}");
        }
        if (!string.IsNullOrEmpty(Domain))
        {
            properties.Add($"axoniq.axonserver.domain={Domain}");
        }
        if (!string.IsNullOrEmpty(InternalDomain))
        {
            properties.Add($"axoniq.axonserver.internal-domain={InternalDomain}");
        }
        if (Port.HasValue)
        {
            properties.Add($"axoniq.axonserver.port={Port.Value}");
        }
        if (ServerPort.HasValue)
        {
            properties.Add($"server.port={ServerPort.Value}");
        }
        if (InternalPort.HasValue)
        {
            properties.Add($"axoniq.axonserver.internal-port={InternalPort.Value}");
        }
        if (Tags != null && Tags.Length != 0)
        {
            properties.Add($"axoniq.axonserver.tags={string.Join(",", Tags.Select(tag => $"{tag.Key}={tag.Value}"))}");
        }
        if (DevModeEnabled.HasValue)
        {
            properties.Add($"axoniq.axonserver.devmode.enabled={DevModeEnabled.Value.ToString().ToLowerInvariant()}");
        }
        if (SetWebSocketAllowedOrigins.HasValue)
        {
            properties.Add($"axoniq.axonserver.set-web-socket-allowed-origins={SetWebSocketAllowedOrigins.Value.ToString().ToLowerInvariant()}");
        }

        return properties.ToArray();

    }

    public void CopyTo(SystemNodeSetup other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.Domain = Domain;
        other.Hostname = Hostname;
        other.Name = Name;
        other.Port = Port;
        other.Tags = Tags?.ToArray();
        other.InternalDomain = InternalDomain;
        other.InternalHostname = InternalHostname;
        other.InternalPort = InternalPort;
        other.ServerPort = ServerPort;
        other.DevModeEnabled = DevModeEnabled;
        other.SetWebSocketAllowedOrigins = SetWebSocketAllowedOrigins;
    }
}