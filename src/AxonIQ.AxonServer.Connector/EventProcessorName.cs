namespace AxonIQ.AxonServer.Connector;

public readonly struct EventProcessorName
{
    private readonly string _value;

    public EventProcessorName(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(EventProcessorName other) => string.Equals(_value, other._value);
    public override bool Equals(object? obj) => obj is EventProcessorName other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}