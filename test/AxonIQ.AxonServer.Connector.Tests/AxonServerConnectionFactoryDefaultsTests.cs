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
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerConnectionFactoryDefaultsTests
{
    [Fact]
    public void PortReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.Port;

        Assert.Equal(8124, result);
    }
    
    [Fact]
    public void RoutingServersReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.RoutingServers;

        Assert.Equal(new List<DnsEndPoint>
        {
            new("localhost", 8124)
        }, result);
    }

    [Fact]
    public void ClientTagsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.ClientTags;

        Assert.Empty(result);
    }

    [Fact]
    public void AuthenticationReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.Authentication;

        Assert.Same(AxonServerAuthentication.None, result);
    }

    [Fact]
    public void ConnectTimeoutReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.ConnectTimeout;

        Assert.Equal(TimeSpan.FromMilliseconds(10_000), result);
    }
    
    [Fact]
    public void MinimumCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.MinimumCommandPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultCommandPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.DefaultCommandPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
    
    [Fact]
    public void MinimumQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.MinimumQueryPermits;

        Assert.Equal(new PermitCount(16), result);
    }
    
    [Fact]
    public void DefaultQueryPermitsReturnsExpectedResult()
    {
        var result = AxonServerConnectionFactoryDefaults.DefaultQueryPermits;

        Assert.Equal(new PermitCount(5_000), result);
    }
}