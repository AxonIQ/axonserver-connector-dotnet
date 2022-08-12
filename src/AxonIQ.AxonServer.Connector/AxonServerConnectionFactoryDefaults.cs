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

using System.Net;

namespace AxonIQ.AxonServer.Connector;

public static class AxonServerConnectionFactoryDefaults
{
    public static readonly int Port = 8124;
    
    public static readonly IReadOnlyCollection<DnsEndPoint> RoutingServers = new[]
        { new DnsEndPoint("localhost", Port) };

    public static readonly IReadOnlyDictionary<string, string> ClientTags = new Dictionary<string, string>();

    public static readonly IAxonServerAuthentication Authentication = AxonServerAuthentication.None;

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(10_000);

    public static readonly PermitCount MinimumCommandPermits = new(16);
    public static readonly PermitCount MinimumQueryPermits = new(16);
    public static readonly PermitCount DefaultQueryPermits = new(5000);
    public static readonly PermitCount DefaultCommandPermits = new(5000);
}