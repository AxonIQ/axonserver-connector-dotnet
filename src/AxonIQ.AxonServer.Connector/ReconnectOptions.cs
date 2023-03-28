namespace AxonIQ.AxonServer.Connector;

public class ReconnectOptions : IEquatable<ReconnectOptions>
{
    public ReconnectOptions(TimeSpan connectionTimeout, TimeSpan reconnectInterval, bool forcePlatformReconnect)
    {
        if(connectionTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(connectionTimeout), connectionTimeout,
                "The connection timeout can not be negative.");
        if(reconnectInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(reconnectInterval), reconnectInterval,
                "The reconnect interval can not be negative.");
        ConnectionTimeout = connectionTimeout;
        ReconnectInterval = reconnectInterval;
        ForcePlatformReconnect = forcePlatformReconnect;
    }
    
    public TimeSpan ConnectionTimeout { get; }
    public TimeSpan ReconnectInterval { get; }
    public bool ForcePlatformReconnect { get; }
    
    public bool Equals(ReconnectOptions? other) =>
        !ReferenceEquals(null, other) && (ReferenceEquals(this, other) ||
                                          ConnectionTimeout.Equals(other.ConnectionTimeout) &&
                                          ReconnectInterval.Equals(other.ReconnectInterval) &&
                                          ForcePlatformReconnect.Equals(other.ForcePlatformReconnect));
    
    public override bool Equals(object? obj) =>
        !ReferenceEquals(null, obj) &&
        (ReferenceEquals(this, obj) || obj is ReconnectOptions other && Equals(other));

    public override int GetHashCode() => HashCode.Combine(ConnectionTimeout, ReconnectInterval, ForcePlatformReconnect);

    public static bool operator ==(ReconnectOptions? left, ReconnectOptions? right) => Equals(left, right);
    public static bool operator !=(ReconnectOptions? left, ReconnectOptions? right) => !Equals(left, right);
}