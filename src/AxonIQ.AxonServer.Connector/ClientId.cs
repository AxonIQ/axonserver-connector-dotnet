namespace AxonIQ.AxonServer.Connector;

public readonly struct ClientId
{
    private readonly string _value;

    public ClientId(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The client id can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(ClientId instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ClientId instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString()
    {
        return _value;
    }

    public static ClientId GenerateFrom(ComponentName component)
    {
        return new ClientId(
            component
                .SuffixWith(new ComponentName("_"))
                .SuffixWith(ComponentName.GenerateRandom(8))
                .ToString()
        );
    }
}