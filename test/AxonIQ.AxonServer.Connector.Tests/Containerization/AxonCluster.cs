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

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public abstract class AxonCluster : IAxonCluster
{
    protected abstract IAxonCluster Cluster { get; }

    public Task InitializeAsync()
    {
        return Cluster.InitializeAsync();
    }

    public IReadOnlyList<IAxonClusterNode> Nodes => Cluster.Nodes;

    public IReadOnlyList<Context> Contexts => Cluster.Contexts;

    public IReadOnlyList<DnsEndPoint> GetHttpEndpoints()
    {
        return Cluster.GetHttpEndpoints();
    }

    public IReadOnlyList<DnsEndPoint> GetGrpcEndpoints()
    {
        return Cluster.GetGrpcEndpoints();
    }

    public Task DisposeAsync()
    {
        return Cluster.DisposeAsync();
    }
}