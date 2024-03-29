using AutoFixture;
using Grpc.Core;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class FaultyCallInvokerTests
{
    private readonly Status _status;
    private readonly Metadata _trailers;
    private readonly string _message;
    private readonly CallInvoker _sut;
    private readonly Fixture _fixture;

    public FaultyCallInvokerTests()
    {
        _fixture = new Fixture();
        _fixture.Customize<Metadata.Entry>(composer =>
            composer
                .FromFactory(() => new Metadata.Entry(_fixture.Create<string>(), _fixture.Create<string>()))
                .OmitAutoProperties()
        );
        _status = new Status(_fixture.Create<StatusCode>(), _fixture.Create<string>());
        _trailers = new Metadata();
        foreach (var entry in _fixture.CreateMany<Metadata.Entry>(Random.Shared.Next(1, 5)))
        {
            _trailers.Add(entry);
        }

        _message = _fixture.Create<string>();
        _sut = new FaultyCallInvoker(_status, _trailers, _message);
    }

    [Fact]
    public void BlockingUnaryCallReturnsExpectedResult()
    {
        var exception = Assert.Throws<RpcException>(() => _sut.BlockingUnaryCall(
            new Method<object, object>(
                MethodType.Unary,
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
            ),
            _fixture.Create<string>(),
            new CallOptions(),
            new object()));
        
        Assert.Equal(_status, exception.Status);
        Assert.Equal(_trailers, exception.Trailers);
        Assert.Equal(_message, exception.Message);
    }
    
    [Fact]
    public void AsyncUnaryCallReturnsExpectedResult()
    {
        var exception = Assert.Throws<RpcException>(() => _sut.AsyncUnaryCall(
            new Method<object, object>(
                MethodType.Unary,
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
            ),
            _fixture.Create<string>(),
            new CallOptions(),
            new object()));
        
        Assert.Equal(_status, exception.Status);
        Assert.Equal(_trailers, exception.Trailers);
        Assert.Equal(_message, exception.Message);
    }
    
    [Fact]
    public void AsyncClientStreamingCallReturnsExpectedResult()
    {
        var exception = Assert.Throws<RpcException>(() => _sut.AsyncClientStreamingCall(
            new Method<object, object>(
                MethodType.Unary,
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
            ),
            _fixture.Create<string>(),
            new CallOptions()));
        
        Assert.Equal(_status, exception.Status);
        Assert.Equal(_trailers, exception.Trailers);
        Assert.Equal(_message, exception.Message);
    }
    
    [Fact]
    public void AsyncServerStreamingCallReturnsExpectedResult()
    {
        var exception = Assert.Throws<RpcException>(() => _sut.AsyncServerStreamingCall(
            new Method<object, object>(
                MethodType.Unary,
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
            ),
            _fixture.Create<string>(),
            new CallOptions(),
            new object()));
        
        Assert.Equal(_status, exception.Status);
        Assert.Equal(_trailers, exception.Trailers);
        Assert.Equal(_message, exception.Message);
    }
    
    [Fact]
    public void AsyncDuplexStreamingCallReturnsExpectedResult()
    {
        var exception = Assert.Throws<RpcException>(() => _sut.AsyncDuplexStreamingCall(
            new Method<object, object>(
                MethodType.Unary,
                _fixture.Create<string>(),
                _fixture.Create<string>(),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
            ),
            _fixture.Create<string>(),
            new CallOptions()));
        
        Assert.Equal(_status, exception.Status);
        Assert.Equal(_trailers, exception.Trailers);
        Assert.Equal(_message, exception.Message);
    }
}