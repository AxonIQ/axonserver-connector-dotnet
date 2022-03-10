using System.Globalization;

namespace AxonIQ.AxonServer.Connector;

public struct LoadFactor : IEquatable<LoadFactor>
{
    private readonly int _value;
    
    public LoadFactor(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "The load factor value can not be less than zero.");
        }

        _value = value;
    }

    public int ToInt32() => _value;
    
    public bool Equals(LoadFactor other) => _value == other._value;

    public override bool Equals(object? obj) => obj is LoadFactor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_value);

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);
    
    public static bool operator ==(LoadFactor left, LoadFactor right) => left.Equals(right);

    public static bool operator !=(LoadFactor left, LoadFactor right) => !left.Equals(right);
}