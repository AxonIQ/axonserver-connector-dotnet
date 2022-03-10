using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public readonly struct Context : IEquatable<Context>
{
    public static readonly Context Admin = new ("_admin");
    public static readonly Context Default = new ("default");
    
    private readonly string _value;

    public Context(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value == string.Empty)
        {
            throw new ArgumentException("The context can not be empty.", nameof(value));
        }
        
        _value = value;
    }

    public bool Equals(Context other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is Context other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;

    public void WriteTo(Metadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        metadata.Add(AxonServerConnectionHeaders.Context, _value);
    }
}