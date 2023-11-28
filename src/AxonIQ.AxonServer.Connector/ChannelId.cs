namespace AxonIQ.AxonServer.Connector;

internal readonly struct ChannelId : IEquatable<ChannelId>
{
    public static ChannelId New() => new(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public ChannelId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The channel identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(ChannelId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ChannelId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
    public static bool operator ==(ChannelId left, ChannelId right) => left.Equals(right);
    public static bool operator !=(ChannelId left, ChannelId right) => !left.Equals(right);
}