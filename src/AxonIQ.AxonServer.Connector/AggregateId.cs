namespace AxonIQ.AxonServer.Connector;

public readonly struct AggregateId : IEquatable<AggregateId>
{
    private readonly string _value;

    public AggregateId(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(AggregateId other) => string.Equals(_value, other._value);
    public override bool Equals(object? obj) => obj is AggregateId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
    public static bool operator ==(AggregateId left, AggregateId right) => left.Equals(right);
    public static bool operator !=(AggregateId left, AggregateId right) => !left.Equals(right);
}