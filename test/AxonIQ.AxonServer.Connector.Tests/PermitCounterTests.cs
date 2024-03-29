using System.Globalization;
using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class PermitCounterTests
{
    private readonly Fixture _fixture;

    public PermitCounterTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void ZeroReturnsExpectedResult()
    {
        Assert.Equal(new PermitCounter(0), PermitCounter.Zero);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void CanNotBeNegative(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PermitCounter(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void CanBePositive(long value)
    {
        var _ = new PermitCounter(value);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void ToInt64ReturnsExpectedResult(long value)
    {
        var sut = new PermitCounter(value);

        var result = sut.ToInt64();
        
        Assert.Equal(value, result);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void ToStringReturnsExpectedResult(long value)
    {
        var sut = new PermitCounter(value);

        var result = sut.ToString();
        
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), result);
    }
    
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, -1)]
    [InlineData(1, 0, 1)]
    public void CompareToReturnsExpectedResult(long leftValue, long rightValue, int expected)
    {
        var left = new PermitCounter(leftValue);
        var right = new PermitCounter(rightValue);

        var result = left.CompareTo(right);

        Assert.Equal(expected, result);
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
        ).Verify(typeof(PermitCounter));
    }
}