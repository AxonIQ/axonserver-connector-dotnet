using System.Globalization;

namespace AxonIQ.AxonServer.Connector;

public readonly struct PermitCount : IEquatable<PermitCount>, IComparable<PermitCount>, IEquatable<PermitCounter>, IComparable<PermitCounter>
{
    private readonly long _value;

    public PermitCount(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Permit count must be greater than 0");
        }

        _value = value;
    }

    public int CompareTo(PermitCount other) => _value.CompareTo(other._value);
    public int CompareTo(PermitCounter other) => _value.CompareTo(other.ToInt64());

    public bool Equals(PermitCount other) => _value.Equals(other._value);
    public bool Equals(PermitCounter other) => _value.Equals(other.ToInt64());
    public override bool Equals(object? obj) => obj is PermitCount other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public long ToInt64() => _value;
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public static PermitCount Max(PermitCount left, PermitCount right) => new(Math.Max(left._value, right._value));
    public static PermitCount Min(PermitCount left, PermitCount right) => new(Math.Min(left._value, right._value));
    
    public static bool operator ==(PermitCount left, PermitCount right) => left.Equals(right);
    public static bool operator !=(PermitCount left, PermitCount right) => !left.Equals(right);
    public static bool operator <(PermitCount left, PermitCount right) => left.CompareTo(right) < 0;
    public static bool operator >(PermitCount left, PermitCount right) => left.CompareTo(right) > 0;
    public static bool operator <=(PermitCount left, PermitCount right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PermitCount left, PermitCount right) => left.CompareTo(right) >= 0;
    
    public static bool operator ==(PermitCount left, PermitCounter right) => left.Equals(right);
    public static bool operator !=(PermitCount left, PermitCounter right) => !left.Equals(right);
    public static bool operator <(PermitCount left, PermitCounter right) => left.CompareTo(right) < 0;
    public static bool operator >(PermitCount left, PermitCounter right) => left.CompareTo(right) > 0;
    public static bool operator <=(PermitCount left, PermitCounter right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PermitCount left, PermitCounter right) => left.CompareTo(right) >= 0;
}