using Microsoft.VisualBasic.CompilerServices;

namespace AxonIQ.AxonServer.Connector;

public readonly struct SubscriptionId
{
    public static SubscriptionId New() => new SubscriptionId(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public SubscriptionId(string value)
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
    
    public bool Equals(SubscriptionId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is SubscriptionId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
    public static bool operator ==(SubscriptionId left, SubscriptionId right) => left.Equals(right);
    public static bool operator !=(SubscriptionId left, SubscriptionId right) => !(left.Equals(right));
}