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

public readonly struct QueryHandlerId
{
    public static QueryHandlerId New() => new QueryHandlerId(Guid.NewGuid().ToString("D"));
    
    private readonly string _value;

    public QueryHandlerId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        
        if (value == "")
        {
            throw new ArgumentException("The query handler identifier can not be empty.", nameof(value));
        }

        _value = value;
    }
    
    public bool Equals(QueryHandlerId other) => other._value.Equals(_value);
    public override bool Equals(object? obj) => obj is QueryHandlerId other && other.Equals(this);
    public override int GetHashCode() => HashCode.Combine(_value);
    public override string ToString() => _value;
}