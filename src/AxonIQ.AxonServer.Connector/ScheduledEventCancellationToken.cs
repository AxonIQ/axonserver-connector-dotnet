namespace AxonIQ.AxonServer.Connector;

public readonly struct ScheduledEventCancellationToken
{
    private readonly string _value;

    public ScheduledEventCancellationToken(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(ScheduledEventCancellationToken other) => string.Equals(_value, other._value);
    public override bool Equals(object? obj) => obj is ScheduledEventCancellationToken other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}