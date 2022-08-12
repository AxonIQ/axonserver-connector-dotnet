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

using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests;

public class MetadataEntryKeyValueComparer : IEqualityComparer<Metadata.Entry>
{
    public bool Equals(Metadata.Entry? x, Metadata.Entry? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.Key == y.Key && x.Value == y.Value;
    }

    public int GetHashCode(Metadata.Entry obj)
    {
        return HashCode.Combine(obj.Key, obj.Value);
    }
}