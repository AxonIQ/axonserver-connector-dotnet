namespace AxonIQ.AxonServer.Connector;

public readonly struct ClientInstanceId
{
    private readonly string _value;

    public ClientInstanceId(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The client instance id can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(ClientInstanceId instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ClientInstanceId instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);

    public override string ToString()
    {
        return _value;
    }

    public static ClientInstanceId GenerateFrom(ComponentName component)
    {
        return new ClientInstanceId(
            component
                .SuffixWith("_")
                .SuffixWith(ComponentName.GenerateRandomSuffix(8))
                .ToString()
        );
    }
}