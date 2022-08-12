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

namespace AxonIQ.AxonServer.Connector;

public readonly struct ClientInstanceId
{
    private readonly string _value;

    public ClientInstanceId(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value == string.Empty) throw new ArgumentException("The client instance id can not be empty", nameof(value));
        _value = value;
    }

    private bool Equals(ClientInstanceId instance) => instance._value.Equals(_value);
    public override bool Equals(object? obj) => obj is ClientInstanceId instance && instance.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);

    public override string ToString()
    {
        return _value;
    }

    public static ClientInstanceId GenerateFrom(ComponentName component)
    {
        return new ClientInstanceId(
            component
                .SuffixWith("_")
                .SuffixWith(ComponentName.GenerateRandomSuffix(8))
                .ToString()
        );
    }
}