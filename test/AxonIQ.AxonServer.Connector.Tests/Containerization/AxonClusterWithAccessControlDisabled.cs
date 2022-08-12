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

using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Containerization;

public class AxonClusterWithAccessControlDisabled : AxonCluster
{
    public AxonClusterWithAccessControlDisabled(IMessageSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        var logger = new MessageSinkLogger<EmbeddedAxonCluster>(sink);
        logger.LogDebug("Using Embedded Axon Cluster with access control disabled");
        Cluster = EmbeddedAxonCluster.WithAccessControlDisabled(logger);
    }

    protected override IAxonCluster Cluster { get; }
}