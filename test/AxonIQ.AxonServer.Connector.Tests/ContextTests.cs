using AutoFixture;
using AutoFixture.Idioms;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
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
            { AxonServerConnectorHeaders.Context, context }
        }, metadata, new MetadataEntryKeyValueComparer());
    }
}