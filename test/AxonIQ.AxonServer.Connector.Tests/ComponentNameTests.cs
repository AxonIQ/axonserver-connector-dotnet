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
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ComponentNameTests
{
    private readonly Fixture _fixture;

    public ComponentNameTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void DefaultReturnsExpectedResult()
    {
        Assert.Equal(new ComponentName("Unnamed"), ComponentName.Default);
    }

    [Fact]
    public void CanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ComponentName(null!));
    }

    [Fact]
    public void CanNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new ComponentName(string.Empty));
    }

    [Fact]
    public void ToStringReturnsExpectedResult()
    {
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

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
        ).Verify(typeof(ComponentName));
    }

    [Fact]
    public void SuffixWithComponentNameReturnsExpectedResult()
    {
        var suffix = _fixture.Create<string>();
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        var result = sut.SuffixWith(new ComponentName(suffix));

        Assert.Equal(new ComponentName(value + suffix), result);
    }

    [Fact]
    public void SuffixWithStringCanNotBeNull()
    {
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        Assert.Throws<ArgumentNullException>(() => sut.SuffixWith(null!));
    }

    [Fact]
    public void SuffixWithStringCanNotBeEmpty()
    {
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        Assert.Throws<ArgumentException>(() => sut.SuffixWith(string.Empty));
    }

    [Fact]
    public void SuffixWithStringReturnsExpectedResult()
    {
        var suffix = _fixture.Create<string>();
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        var result = sut.SuffixWith(suffix);

        Assert.Equal(new ComponentName(value + suffix), result);
    }

    [Fact]
    public void PrefixWithComponentNameReturnsExpectedResult()
    {
        var prefix = _fixture.Create<string>();
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        var result = sut.PrefixWith(new ComponentName(prefix));

        Assert.Equal(new ComponentName(prefix + value), result);
    }

    [Fact]
    public void PrefixWithStringCanNotBeNull()
    {
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        Assert.Throws<ArgumentNullException>(() => sut.PrefixWith(null!));
    }

    [Fact]
    public void PrefixWithStringCanNotBeEmpty()
    {
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        Assert.Throws<ArgumentException>(() => sut.PrefixWith(string.Empty));
    }

    [Fact]
    public void PrefixWithStringReturnsExpectedResult()
    {
        var prefix = _fixture.Create<string>();
        var value = _fixture.Create<string>();
        var sut = new ComponentName(value);

        var result = sut.PrefixWith(prefix);

        Assert.Equal(new ComponentName(prefix + value), result);
    }

    [Fact]
    public void GenerateRandomSuffixLengthCanNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentName.GenerateRandomSuffix(-1));
    }

    [Fact]
    public void GenerateRandomSuffixLengthCanNotBeZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentName.GenerateRandomSuffix(0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void GenerateRandomSuffixReturnsExpectedResult(int length)
    {
        var hexCharacters = new[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f'
        };
        var result = ComponentName.GenerateRandomSuffix(length);

        Assert.Equal(length, result.ToString().Length);
        Assert.All(result.ToString(), character =>
            Assert.Contains(character, hexCharacters));
    }

    [Fact]
    public void GenerateRandomNameReturnsExpectedResult()
    {
        var hexCharacters = new[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f'
        };
        var result = ComponentName.GenerateRandomName();

        Assert.StartsWith(ComponentName.Default.SuffixWith("_").ToString(), result.ToString());
        Assert.Equal(ComponentName.Default.SuffixWith("_").ToString().Length + 4, result.ToString().Length);
        Assert.All(result.ToString().Substring(ComponentName.Default.SuffixWith("_").ToString().Length),
            character => Assert.Contains(character, hexCharacters));
    }
}