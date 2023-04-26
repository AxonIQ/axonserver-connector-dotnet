namespace AxonIQ.AxonServer.Connector;

public readonly struct InstructionId
{
    private readonly string _value;

    public static InstructionId New() => new (Guid.NewGuid().ToString("D"));

    public static bool CanParse(string value) => !string.IsNullOrEmpty(value);

    public static bool TryParse(string value, out InstructionId parsed)
    {
        if (string.IsNullOrEmpty(value))
        {
            parsed = default;
            return false;
        }

        parsed = new InstructionId(value);
        return true;
    }

    public static InstructionId Parse(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        
        if (string.IsNullOrEmpty(value))
        {
            throw new FormatException("The instruction identifier can not be null or empty.");
        }

        return new InstructionId(value);
    }
    
    private InstructionId(string value)
    {
        _value = value;
    }
    
    public bool Equals(InstructionId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is InstructionId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}