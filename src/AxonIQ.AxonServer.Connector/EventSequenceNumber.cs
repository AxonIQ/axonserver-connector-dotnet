namespace AxonIQ.AxonServer.Connector;

public readonly struct EventSequenceNumber
{
    private readonly long _value;

    public EventSequenceNumber(long value)
    {
        _value = value;
    }

    public bool Equals(EventSequenceNumber other) => _value.Equals(other._value);
    public override bool Equals(object? obj) => obj is EventSequenceNumber other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public long ToInt64() => _value;
}