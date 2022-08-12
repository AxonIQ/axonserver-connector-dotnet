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

using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryConfigurationTests
{
    [Fact]
    public void DefaultSectionReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.DefaultSection;

        Assert.Equal("AxonIQ", result);
    }

    [Fact]
    public void ComponentNameReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.ComponentName;

        Assert.Equal("ComponentName", result);
    }

    [Fact]
    public void ClientInstanceIdReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.ClientInstanceId;

        Assert.Equal("ClientInstanceId", result);
    }

    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryConfiguration.RoutingServers;

        Assert.Equal("RoutingServers", result);
    }
}