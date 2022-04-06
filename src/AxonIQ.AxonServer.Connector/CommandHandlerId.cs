namespace AxonIQ.AxonServer.Connector;

public readonly struct CommandHandlerId
{
    public static CommandHandlerId New() => new CommandHandlerId(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public CommandHandlerId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The command handler identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(CommandHandlerId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is CommandHandlerId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}