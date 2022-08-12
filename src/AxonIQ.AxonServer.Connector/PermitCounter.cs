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

public readonly struct PermitCounter : IEquatable<PermitCounter>, IComparable<PermitCounter>
{
    public static readonly PermitCounter Zero = new(0L);
    
    private readonly long _value;

    public PermitCounter(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Permit counter must be greater than or equal to 0");
        }

        _value = value;
    }

    public PermitCounter Increment()
    {
        if (_value == long.MaxValue)
        {
            throw new InvalidOperationException(
                $"Permit counter can not be incremented because it has reached its maximum value of {long.MaxValue}.");
        }

        return new PermitCounter(_value + 1L);
    }

    public int CompareTo(PermitCounter other) => _value.CompareTo(other._value);

    public bool Equals(PermitCounter other) => _value.Equals(other._value);
    public override bool Equals(object? obj) => obj is PermitCounter other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public long ToInt64() => _value;
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);
    public static bool operator ==(PermitCounter left, PermitCounter right) => left.Equals(right);
    public static bool operator !=(PermitCounter left, PermitCounter right) => !left.Equals(right);
    public static bool operator <(PermitCounter left, PermitCounter right) => left.CompareTo(right) < 0;
    public static bool operator >(PermitCounter left, PermitCounter right) => left.CompareTo(right) > 0;
    public static bool operator <=(PermitCounter left, PermitCounter right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PermitCounter left, PermitCounter right) => left.CompareTo(right) >= 0;
}