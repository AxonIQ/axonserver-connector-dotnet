using System.Globalization;
using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class PermitCountTests
{
    private readonly Fixture _fixture;

    public PermitCountTests()
    {
        _fixture = new Fixture();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void CanNotBeZeroOrNegative(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PermitCount(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void CanBePositive(long value)
    {
        var _ = new PermitCount(value);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void ToInt64ReturnsExpectedResult(long value)
    {
        var sut = new PermitCount(value);

        var result = sut.ToInt64();
        
        Assert.Equal(value, result);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void ToStringReturnsExpectedResult(long value)
    {
        var sut = new PermitCount(value);

        var result = sut.ToString();
        
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), result);
    }
    
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 2, -1)]
    [InlineData(2, 1, 1)]
    public void CompareToPermitCountReturnsExpectedResult(long leftValue, long rightValue, int expected)
    {
        var left = new PermitCount(leftValue);
        var right = new PermitCount(rightValue);

        var result = left.CompareTo(right);

        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1, 2, -1)]
    [InlineData(2, 1, 1)]
    public void CompareToPermitCounterReturnsExpectedResult(long leftValue, long rightValue, int expected)
    {
        var left = new PermitCount(leftValue);
        var right = new PermitCounter(rightValue);

        var result = left.CompareTo(right);

        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 2)]
    public void MaxReturnsExpectedResult(long leftValue, long rightValue, long expected)
    {
        var left = new PermitCount(leftValue);
        var right = new PermitCount(rightValue);

        var result = PermitCount.Max(left, right);

        Assert.Equal(new PermitCount(expected), result);
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
        ).Verify(typeof(PermitCount));
    }
}