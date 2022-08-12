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

using AutoFixture;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ClientIdentityExtensionsTests
{
    [Fact]
    public void ToClientIdentificationReturnsExpectedResult()
    {
        var fixture = new Fixture();
        fixture.CustomizeClientInstanceId();
        fixture.CustomizeComponentName();

        var component = fixture.Create<ComponentName>();
        var clientInstanceId = fixture.Create<ClientInstanceId>();
        var tags = fixture.Create<Dictionary<string, string>>();
        var version = fixture.Create<Version>();

        var sut = new ClientIdentity(component, clientInstanceId, tags, version);

        var result = sut.ToClientIdentification();

        Assert.Equal(component.ToString(), result.ComponentName);
        Assert.Equal(clientInstanceId.ToString(), result.ClientId);
        Assert.Equal(tags, result.Tags);
        Assert.Equal(version.ToString(), result.Version);
    }
}