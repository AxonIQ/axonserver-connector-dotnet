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
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public interface IAxonServerConnectionFactoryOptionsBuilder
{
    IAxonServerConnectionFactoryOptionsBuilder AsComponentName(ComponentName name);
    IAxonServerConnectionFactoryOptionsBuilder AsClientInstanceId(ClientInstanceId id);
    IAxonServerConnectionFactoryOptionsBuilder WithDefaultRoutingServers();
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(params DnsEndPoint[] servers);
    IAxonServerConnectionFactoryOptionsBuilder WithRoutingServers(IEnumerable<DnsEndPoint> servers);
    IAxonServerConnectionFactoryOptionsBuilder WithoutAuthentication();
    IAxonServerConnectionFactoryOptionsBuilder WithAuthenticationToken(string token);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTag(string key, string value);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(params KeyValuePair<string, string>[] tags);
    IAxonServerConnectionFactoryOptionsBuilder WithClientTags(IEnumerable<KeyValuePair<string, string>> tags);
    IAxonServerConnectionFactoryOptionsBuilder WithLoggerFactory(ILoggerFactory loggerFactory);
    IAxonServerConnectionFactoryOptionsBuilder WithClock(Func<DateTimeOffset> clock);
    IAxonServerConnectionFactoryOptionsBuilder WithGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions);
    IAxonServerConnectionFactoryOptionsBuilder WithCommandPermits(PermitCount count);
    IAxonServerConnectionFactoryOptionsBuilder WithQueryPermits(PermitCount count);

    AxonServerConnectionFactoryOptions Build();
}