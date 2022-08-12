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
using AxonIQ.AxonServer.Connector.Tests.Containerization;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
public class AxonServerConnectionFactoryIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;

    public AxonServerConnectionFactoryIntegrationTests(AxonServerWithAccessControlDisabled container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeContext();
    }

    private AxonServerConnectionFactory CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint());
        configure?.Invoke(builder);
        var options = builder.Build();
        return new AxonServerConnectionFactory(options);
    }

    [Fact]
    public async Task ConnectContextReturnsExpectedResult()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var result = await sut.Connect(context);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SuccessiveConnectToSameContextReturnsSameInstance()
    {
        var context = _fixture.Create<Context>();
        var sut = CreateSystemUnderTest();

        var first = await sut.Connect(context);
        var second = await sut.Connect(context);

        Assert.Same(first, second);
    }
}