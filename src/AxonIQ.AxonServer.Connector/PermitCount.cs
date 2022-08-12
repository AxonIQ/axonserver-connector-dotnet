/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

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