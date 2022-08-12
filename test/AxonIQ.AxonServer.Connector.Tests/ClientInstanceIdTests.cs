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

public class ClientInstanceIdTests
{
    private readonly Fixture _fixture;

    public ClientInstanceIdTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void CanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ClientInstanceId(null!));
    }

    [Fact]
    public void CanNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new ClientInstanceId(string.Empty));
    }

    [Fact]
    public void ToStringReturnsExpectedResult()
    {
        var value = _fixture.Create<string>();
        var sut = new ClientInstanceId(value);

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
        ).Verify(typeof(ClientInstanceId));
    }
}