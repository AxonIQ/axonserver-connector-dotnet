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
using AutoFixture.Idioms;
using Grpc.Core;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ContextTests
{
    private readonly Fixture _fixture;

    public ContextTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void AdminReturnsExpectedResult()
    {
        Assert.Equal(new Context("_admin"), Context.Admin);
    }
    
    [Fact]
    public void DefaultReturnsExpectedResult()
    {
        Assert.Equal(new Context("default"), Context.Default);
    }

    [Fact]
    public void CanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Context(null!));
    }

    [Fact]
    public void CanNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new Context(string.Empty));
    }

    [Fact]
    public void ToStringReturnsExpectedResult()
    {
        var value = _fixture.Create<string>();
        var sut = new Context(value);

        var result = sut.ToString();

        Assert.Equal(value, result);
    }

    [Fact]
    public void VerifyEquality()
    {
        new CompositeIdiomaticAssertion(
            new EqualsNullAssertion(_fixture),
            new EqualsSelfAssertion(_fixture),
            new EqualsSuccessiveAssertion(_fixture),
            new EqualsNewObjectAssertion(_fixture),
            new GetHashCodeSuccessiveAssertion(_fixture)
        ).Verify(typeof(Context));
    }

    [Fact]
    public void WriteToMetadataCanNotBeNull()
    {
        var sut = _fixture.Create<Context>();
        Assert.Throws<ArgumentNullException>(() => sut.WriteTo(null!));
    }

    [Fact]
    public void WriteToMetadataHasExpectedResult()
    {
        var context = _fixture.Create<string>();

        var sut = new Context(context);

        var metadata = new Metadata();
        sut.WriteTo(metadata);

        Assert.Equal(new Metadata
        {
            { AxonServerConnectionHeaders.Context, context }
        }, metadata, new MetadataEntryKeyValueComparer());
    }
}