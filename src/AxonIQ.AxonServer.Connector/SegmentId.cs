using System.Globalization;

namespace AxonIQ.AxonServer.Connector;

public readonly struct SegmentId
{
    private readonly int _value;

    public SegmentId(int value)
    {
        _value = value;
    }

    public bool Equals(SegmentId other) => _value.Equals(other._value);
    public override bool Equals(object? obj) => obj is SegmentId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);
    public int ToInt32() => _value;
}