namespace AxonIQ.AxonServer.Connector;

public readonly struct RegistrationId
{
    public static RegistrationId New() => new RegistrationId(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public RegistrationId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The registration identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(RegistrationId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is RegistrationId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
    public static bool operator ==(RegistrationId left, RegistrationId right) => left.Equals(right);
    public static bool operator !=(RegistrationId left, RegistrationId right) => !(left.Equals(right));
}