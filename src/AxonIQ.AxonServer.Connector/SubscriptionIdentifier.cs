namespace AxonIQ.AxonServer.Connector;

public readonly struct SubscriptionIdentifier
{
    public static SubscriptionIdentifier New() => new SubscriptionIdentifier(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public SubscriptionIdentifier(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The subscription identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(SubscriptionIdentifier other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is SubscriptionIdentifier other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
    public static bool operator ==(SubscriptionIdentifier left, SubscriptionIdentifier right) => left.Equals(right);
    public static bool operator !=(SubscriptionIdentifier left, SubscriptionIdentifier right) => !(left.Equals(right));
}