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

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class AxonServer : IAxonServer
{
    protected abstract IAxonServer Server { get; }

    public Task InitializeAsync()
    {
        return Server.InitializeAsync();
    }

    public SystemProperties Properties => Server.Properties;

    public DnsEndPoint GetHttpEndpoint()
    {
        return Server.GetHttpEndpoint();
    }

    public HttpClient CreateHttpClient()
    {
        return Server.CreateHttpClient();
    }

    public DnsEndPoint GetGrpcEndpoint()
    {
        return Server.GetGrpcEndpoint();
    }

    public GrpcChannel CreateGrpcChannel(GrpcChannelOptions? options = default)
    {
        return Server.CreateGrpcChannel(options);
    }

    public async Task PurgeEvents()
    {
        using var client = Server.CreateHttpClient();
        (await client.DeleteAsync("v1/devmode/purge-events")).EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        return Server.DisposeAsync();
    }
}