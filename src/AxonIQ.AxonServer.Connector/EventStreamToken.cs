using System.Globalization;

namespace AxonIQ.AxonServer.Connector;

public readonly struct EventStreamToken : IEquatable<EventStreamToken>
{
    private readonly long _value;

    public static readonly EventStreamToken None = new(-1);

    public EventStreamToken(long value)
    {
        _value = value;
    }

    public bool Equals(EventStreamToken other) => _value.Equals(other._value);
    public override bool Equals(object? obj) => obj is EventStreamToken other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public long ToInt64() => _value;

    public static bool operator ==(EventStreamToken left, EventStreamToken right) => left.Equals(right);
    public static bool operator !=(EventStreamToken left, EventStreamToken right) => !left.Equals(right);
}