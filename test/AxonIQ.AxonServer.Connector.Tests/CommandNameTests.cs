using AutoFixture;
using AutoFixture.Idioms;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CommandNameTests
{
    private readonly Fixture _fixture;

    public CommandNameTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void CanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CommandName(null!));
    }

    [Fact]
    public void CanNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CommandName(string.Empty));
    }

    [Fact]
    public void ToStringReturnsExpectedResult()
    {
        var value = _fixture.Create<string>();
        var sut = new CommandName(value);

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
        ).Verify(typeof(CommandName));
    }
}