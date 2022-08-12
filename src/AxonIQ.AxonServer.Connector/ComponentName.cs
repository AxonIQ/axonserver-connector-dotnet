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

using System.Diagnostics.Contracts;

namespace AxonIQ.AxonServer.Connector;

public readonly struct ComponentName
{
    public static readonly ComponentName Default = new("Unnamed");

    private readonly string _value;

    public ComponentName(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The component name can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(ComponentName instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ComponentName instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);

    public override string ToString()
    {
        return _value;
    }

    [Pure]
    public ComponentName SuffixWith(string suffix)
    {
        return SuffixWith(new ComponentName(suffix));
    }

    [Pure]
    public ComponentName SuffixWith(ComponentName suffix)
    {
        return new ComponentName(_value + suffix._value);
    }

    [Pure]
    public ComponentName PrefixWith(string prefix)
    {
        return PrefixWith(new ComponentName(prefix));
    }

    [Pure]
    public ComponentName PrefixWith(ComponentName prefix)
    {
        return new ComponentName(prefix._value + _value);
    }

    private static readonly char[] HexCharacters =
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'a', 'b', 'c', 'd', 'e', 'f'
    };

    public static ComponentName GenerateRandomName()
    {
        return Default.SuffixWith("_").SuffixWith(GenerateRandomSuffix(4));
    }

    internal static ComponentName GenerateRandomSuffix(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length,
                "The component name length can not be negative or zero");
        return new ComponentName(
            new string(
                Enumerable
                    .Range(0, length)
                    .Select(_ => HexCharacters[Random.Shared.Next(0, 16)])
                    .ToArray()));
    }
}