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
using Grpc.Core;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonServerAuthenticationTests
{
    private readonly Fixture _fixture;

    public AxonServerAuthenticationTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void NoneReturnsExpectedResult()
    {
        Assert.IsType<NoServerAuthentication>(AxonServerAuthentication.None);
    }

    [Fact]
    public void NoneActsAsSingleton()
    {
        Assert.Same(AxonServerAuthentication.None, AxonServerAuthentication.None);
    }

    [Fact]
    public void UsingTokenReturnsExpectedResult()
    {
        var token = _fixture.Create<string>();

        var authentication = Assert.IsType<TokenBasedServerAuthentication>(AxonServerAuthentication.UsingToken(token));

        Assert.Equal(token, authentication.Token);
    }

    [Fact]
    public void NoServerAuthenticationAuthenticationWriteToMetadataCanNotBeNull()
    {
        var sut = new NoServerAuthentication();
        Assert.Throws<ArgumentNullException>(() => sut.WriteTo(null!));
    }

    [Fact]
    public void NoServerAuthenticationWriteToMetadataHasExpectedResult()
    {
        var sut = new NoServerAuthentication();

        var metadata = new Metadata();
        sut.WriteTo(metadata);

        Assert.Empty(metadata);
    }

    [Fact]
    public void TokenBasedServerAuthenticationWriteToMetadataCanNotBeNull()
    {
        var token = _fixture.Create<string>();
        var sut = new TokenBasedServerAuthentication(token);
        Assert.Throws<ArgumentNullException>(() => sut.WriteTo(null!));
    }

    [Fact]
    public void TokenBasedServerAuthenticationWriteToMetadataHasExpectedResult()
    {
        var token = _fixture.Create<string>();

        var sut = new TokenBasedServerAuthentication(token);

        var metadata = new Metadata();
        sut.WriteTo(metadata);

        Assert.Equal(new Metadata
        {
            { AxonServerConnectionHeaders.AccessToken, token }
        }, metadata, new MetadataEntryKeyValueComparer());
    }
}