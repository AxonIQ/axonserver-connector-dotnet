namespace AxonIQ.AxonServer.Embedded;

public class SystemKeepAlive
{
    /// <summary>
    /// Interval at which AxonServer will send timeout messages. Set to 0 to disable gRPC timeout checks. Default value is 2500.
    /// </summary>
    public int? KeepAliveTime { get; set; }
    /// <summary>
    /// Timeout (in ms.) for keep alive messages on gRPC connections. Default value is 5000.
    /// </summary>
    public int? KeepAliveTimeout { get; set; }
    /// <summary>
    /// Minimum keep alive interval (in ms.) accepted by this end of the gRPC connection. Default value is 1000.
    /// </summary>
    public int? MinKeepAliveTime { get; set; }
    /// <summary>
    /// Timeout (in ms.) on application level heartbeat between client and Axon Server. Default value is 5000.
    /// </summary>
    public int? ClientHeartbeatTimeout { get; set; }
    /// <summary>
    /// Initial time delay (in ms.) before Axon Server checks for heartbeats from clients. Default value is 10000.
    /// </summary>
    public int? ClientHeartbeatCheckInitialDelay { get; set; }
    /// <summary>
    /// How often (in ms.) does Axon Server check for heartbeats from clients. Default value is 1000.
    /// </summary>
    public int? ClientHeartbeatCheckRate { get; set; }
    /// <summary>
    /// If this is set Axon Server will respond to heartbeats from clients and send heartbeat. Default value is false.
    /// </summary>
    public bool? HeartbeatEnabled { get; set; }
    
    public string[] Serialize()
    {
        var properties = new List<string>();
        if (HeartbeatEnabled.HasValue)
        {
            properties.Add($"axoniq.axonserver.heartbeat.enabled={HeartbeatEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (KeepAliveTime.HasValue)
        {
            properties.Add($"axoniq.axonserver.keep-alive-time={KeepAliveTime.Value}");
        }
        
        if (KeepAliveTimeout.HasValue)
        {
            properties.Add($"axoniq.axonserver.keep-alive-timeout={KeepAliveTimeout.Value}");
        }
        
        if (MinKeepAliveTime.HasValue)
        {
            properties.Add($"axoniq.axonserver.min-keep-alive-time={MinKeepAliveTime.Value}");
        }
        
        if (ClientHeartbeatTimeout.HasValue)
        {
            properties.Add($"axoniq.axonserver.client-heartbeat-timeout={ClientHeartbeatTimeout.Value}");
        }
        
        if (ClientHeartbeatCheckInitialDelay.HasValue)
        {
            properties.Add($"axoniq.axonserver.client-heartbeat-check-initial-delay={ClientHeartbeatCheckInitialDelay.Value}");
        }
        
        if (ClientHeartbeatCheckRate.HasValue)
        {
            properties.Add($"axoniq.axonserver.client-heartbeat-check-rate={ClientHeartbeatCheckRate.Value}");
        }
        
        return properties.ToArray();
    }

    public void CopyTo(SystemKeepAlive other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        other.HeartbeatEnabled = HeartbeatEnabled;
        other.ClientHeartbeatTimeout = ClientHeartbeatTimeout;
        other.KeepAliveTime = KeepAliveTime;
        other.KeepAliveTimeout = KeepAliveTimeout;
        other.ClientHeartbeatCheckRate = ClientHeartbeatCheckRate;
        other.MinKeepAliveTime = MinKeepAliveTime;
        other.ClientHeartbeatCheckInitialDelay = ClientHeartbeatCheckInitialDelay;
    }
}