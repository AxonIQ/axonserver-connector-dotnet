namespace AxonIQ.AxonServer.Connector;

public struct CommandName
{
    private readonly string _value;

    public CommandName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The command name can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(CommandName other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is CommandName other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}