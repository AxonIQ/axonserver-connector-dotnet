namespace AxonIQ.AxonServer.Connector;

public readonly struct InstructionId
{
    private readonly string _value;

    public static InstructionId New() => new InstructionId(Guid.NewGuid().ToString("D"));
    
    public InstructionId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The instruction identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(InstructionId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is InstructionId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}