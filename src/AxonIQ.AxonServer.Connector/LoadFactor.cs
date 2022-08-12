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