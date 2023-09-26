namespace AxonIQ.AxonServer.Connector;

public readonly struct RegisteredQueryId : IEquatable<RegisteredQueryId>
{
    private readonly Guid _value;
    
    public static RegisteredQueryId New() => new(Guid.NewGuid());

    private RegisteredQueryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The query id can not be empty.", nameof(value));
        }

        _value = value;
    }

    public bool Equals(RegisteredQueryId other) => _value.Equals(other._value);
    
    public override bool Equals(object? obj) => obj is RegisteredQueryId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value.GetHashCode());
    public override string ToString() => _value.ToString("N");

    public static bool operator ==(RegisteredQueryId left, RegisteredQueryId right) => left.Equals(right);
    public static bool operator !=(RegisteredQueryId left, RegisteredQueryId right) => !(left == right);
}