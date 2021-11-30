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
    public void GenerateRandomLengthCanNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentName.GenerateRandom(-1));
    }
    
    [Fact]
    public void GenerateRandomLengthCanNotBeZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ComponentName.GenerateRandom(0));
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void GenerateRandomReturnsExpectedResult(int length)
    {
        var hexCharacters = new[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f'
        };
        var result = ComponentName.GenerateRandom(length);
        
        Assert.Equal(length, result.ToString().Length);
        Assert.All(result.ToString(), character =>
            Assert.Contains(character, hexCharacters));
    }
}