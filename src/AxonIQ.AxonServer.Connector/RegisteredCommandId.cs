namespace AxonIQ.AxonServer.Connector;

public readonly struct RegisteredCommandId : IEquatable<RegisteredCommandId>
{
    private readonly Guid _value;
    
    public static RegisteredCommandId New() => new(Guid.NewGuid());

    private RegisteredCommandId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The command id can not be empty.", nameof(value));
        }

        _value = value;
    }

    public bool Equals(RegisteredCommandId other) => _value.Equals(other._value);
    
    public override bool Equals(object? obj) => obj is RegisteredCommandId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value.GetHashCode());
    public override string ToString() => _value.ToString("N");

    public static bool operator ==(RegisteredCommandId left, RegisteredCommandId right) => left.Equals(right);
    public static bool operator !=(RegisteredCommandId left, RegisteredCommandId right) => !(left == right);
}