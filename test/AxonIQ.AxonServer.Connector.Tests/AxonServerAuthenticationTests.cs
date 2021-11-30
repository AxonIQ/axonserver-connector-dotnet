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
    public void NoServerAuthenticationWriteToMetadataHasExpectedResult()
    {
        var sut = new NoServerAuthentication();

        var metadata = new Metadata();
        sut.WriteTo(metadata);
        
        Assert.Empty(metadata);
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
    
    private class MetadataEntryKeyValueComparer : IEqualityComparer<Metadata.Entry>
    {
        public bool Equals(Metadata.Entry? x, Metadata.Entry? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Key == y.Key && x.Value == y.Value;
        }

        public int GetHashCode(Metadata.Entry obj)
        {
            return HashCode.Combine(obj.Key, obj.Value);
        }
    }
}