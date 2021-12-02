using Grpc.Core;

namespace AxonIQ.AxonServer.Connector;

public readonly struct Context
{
    private readonly string _value;

    public Context(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The context can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(Context instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is Context instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);

    public void WriteTo(Metadata metadata)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        metadata.Add(AxonServerConnectionHeaders.Context, _value);
    }

    public override string ToString()
    {
        return _value;
    }
}