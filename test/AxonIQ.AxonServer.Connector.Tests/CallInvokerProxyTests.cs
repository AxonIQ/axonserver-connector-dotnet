using AutoFixture;
using Grpc.Core;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CallInvokerProxyTests
{
    public class WhenServiceAvailable
    {
        private readonly Fixture _fixture;
        private readonly CountingCallInvoker _invoker;
        private readonly CallInvokerProxy _sut;

        public WhenServiceAvailable()
        {
            _fixture = new Fixture();
            _invoker = new CountingCallInvoker();
            _sut = new CallInvokerProxy(() => _invoker);
        }
        
        [Fact]
        public void BlockingUnaryCallReturnsExpectedResult()
        {
            _sut.BlockingUnaryCall(
                new Method<object, object>(
                    MethodType.Unary,
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                ),
                _fixture.Create<string>(),
                new CallOptions(),
                new object());

            Assert.Equal(1, _invoker.BlockingUnaryCalls);
            Assert.Equal(0, _invoker.AsyncUnaryCalls);
            Assert.Equal(0, _invoker.AsyncClientStreamingCalls);
            Assert.Equal(0, _invoker.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _invoker.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncUnaryCallReturnsExpectedResult()
        {
            _sut.AsyncUnaryCall(
                new Method<object, object>(
                    MethodType.Unary,
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                ),
                _fixture.Create<string>(),
                new CallOptions(),
                new object());
            
            Assert.Equal(0, _invoker.BlockingUnaryCalls);
            Assert.Equal(1, _invoker.AsyncUnaryCalls);
            Assert.Equal(0, _invoker.AsyncClientStreamingCalls);
            Assert.Equal(0, _invoker.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _invoker.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncClientStreamingCallReturnsExpectedResult()
        {
            _sut.AsyncClientStreamingCall(
                new Method<object, object>(
                    MethodType.Unary,
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                ),
                _fixture.Create<string>(),
                new CallOptions());
            
            Assert.Equal(0, _invoker.BlockingUnaryCalls);
            Assert.Equal(0, _invoker.AsyncUnaryCalls);
            Assert.Equal(1, _invoker.AsyncClientStreamingCalls);
            Assert.Equal(0, _invoker.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _invoker.AsyncServerStreamingCalls);
        }

        [Fact]
        public void AsyncDuplexStreamingCallReturnsExpectedResult()
        {
            _sut.AsyncDuplexStreamingCall(
                new Method<object, object>(
                    MethodType.Unary,
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                ),
                _fixture.Create<string>(),
                new CallOptions());

            Assert.Equal(0, _invoker.BlockingUnaryCalls);
            Assert.Equal(0, _invoker.AsyncUnaryCalls);
            Assert.Equal(0, _invoker.AsyncClientStreamingCalls);
            Assert.Equal(1, _invoker.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _invoker.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncServerStreamingCallReturnsExpectedResult()
        {
            _sut.AsyncServerStreamingCall(
                new Method<object, object>(
                    MethodType.Unary,
                    _fixture.Create<string>(),
                    _fixture.Create<string>(),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                    new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                ),
                _fixture.Create<string>(),
                new CallOptions(),
                new object());

            Assert.Equal(0, _invoker.BlockingUnaryCalls);
            Assert.Equal(0, _invoker.AsyncUnaryCalls);
            Assert.Equal(0, _invoker.AsyncClientStreamingCalls);
            Assert.Equal(0, _invoker.AsyncDuplexStreamingCalls);
            Assert.Equal(1, _invoker.AsyncServerStreamingCalls);
        }

        
        private class CountingCallInvoker : CallInvoker
        {
            public int BlockingUnaryCalls { get; private set; }
            
            public int AsyncUnaryCalls { get; private set; }
            
            public int AsyncServerStreamingCalls { get; private set; }
            public int AsyncClientStreamingCalls { get; private set; }
            public int AsyncDuplexStreamingCalls { get; private set; }
        
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                BlockingUnaryCalls++;
                return null!;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                AsyncUnaryCalls++;
                return null!;
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options,
                TRequest request)
            {
                AsyncServerStreamingCalls++;
                return null!;
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                AsyncClientStreamingCalls++;
                return null!;
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                AsyncDuplexStreamingCalls++;
                return null!;
            }
        }
    }

    public class WhenServiceNotAvailable
    {
        private readonly Fixture _fixture;
        private readonly CallInvokerProxy _sut;
        private readonly Status _status;
        private readonly Metadata _trailers;
        private readonly string _message;

        public WhenServiceNotAvailable()
        {
            _fixture = new Fixture();
            _sut = new CallInvokerProxy(() => null);
            _status = new Status(StatusCode.Unavailable, nameof(StatusCode.Unavailable));
            _trailers = Metadata.Empty;
            _message = "The axon server is not ready to handle the request";
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
    }

    public class WhenServiceGotSwapped
    {
        private readonly Fixture _fixture;
        private readonly CountingCallInvoker _first;
        private readonly CountingCallInvoker _next;
        private readonly CallInvokerProxy _sut;
        private int _callCount;

        public WhenServiceGotSwapped()
        {
            _fixture = new Fixture();
            _first = new CountingCallInvoker();
            _next = new CountingCallInvoker();
            
            _sut = new CallInvokerProxy(() => Interlocked.Increment(ref _callCount) == 1 ? _first : _next);
        }
        
        [Fact]
        public void BlockingUnaryCallReturnsExpectedResult()
        {
            var callCount = Random.Shared.Next(1, 5);
            for (var index = 0; index < callCount; index++)
            {
                _sut.BlockingUnaryCall(
                    new Method<object, object>(
                        MethodType.Unary,
                        _fixture.Create<string>(),
                        _fixture.Create<string>(),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                    ),
                    _fixture.Create<string>(),
                    new CallOptions(),
                    new object());
            }

            Assert.Equal(1, _first.BlockingUnaryCalls);
            Assert.Equal(0, _first.AsyncUnaryCalls);
            Assert.Equal(0, _first.AsyncClientStreamingCalls);
            Assert.Equal(0, _first.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _first.AsyncServerStreamingCalls);
            
            Assert.Equal(callCount - 1, _next.BlockingUnaryCalls);
            Assert.Equal(0, _next.AsyncUnaryCalls);
            Assert.Equal(0, _next.AsyncClientStreamingCalls);
            Assert.Equal(0, _next.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _next.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncUnaryCallReturnsExpectedResult()
        {
            var callCount = Random.Shared.Next(1, 5);
            for (var index = 0; index < callCount; index++)
            {
                _sut.AsyncUnaryCall(
                    new Method<object, object>(
                        MethodType.Unary,
                        _fixture.Create<string>(),
                        _fixture.Create<string>(),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                    ),
                    _fixture.Create<string>(),
                    new CallOptions(),
                    new object());
            }

            Assert.Equal(0, _first.BlockingUnaryCalls);
            Assert.Equal(1, _first.AsyncUnaryCalls);
            Assert.Equal(0, _first.AsyncClientStreamingCalls);
            Assert.Equal(0, _first.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _first.AsyncServerStreamingCalls);
            
            Assert.Equal(0, _next.BlockingUnaryCalls);
            Assert.Equal(callCount - 1, _next.AsyncUnaryCalls);
            Assert.Equal(0, _next.AsyncClientStreamingCalls);
            Assert.Equal(0, _next.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _next.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncClientStreamingCallReturnsExpectedResult()
        {
            var callCount = Random.Shared.Next(1, 5);
            for (var index = 0; index < callCount; index++)
            {
                _sut.AsyncClientStreamingCall(
                    new Method<object, object>(
                        MethodType.Unary,
                        _fixture.Create<string>(),
                        _fixture.Create<string>(),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                    ),
                    _fixture.Create<string>(),
                    new CallOptions());
            }

            Assert.Equal(0, _first.BlockingUnaryCalls);
            Assert.Equal(0, _first.AsyncUnaryCalls);
            Assert.Equal(1, _first.AsyncClientStreamingCalls);
            Assert.Equal(0, _first.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _first.AsyncServerStreamingCalls);
            
            Assert.Equal(0, _next.BlockingUnaryCalls);
            Assert.Equal(0, _next.AsyncUnaryCalls);
            Assert.Equal(callCount - 1, _next.AsyncClientStreamingCalls);
            Assert.Equal(0, _next.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _next.AsyncServerStreamingCalls);
        }

        [Fact]
        public void AsyncDuplexStreamingCallReturnsExpectedResult()
        {
            var callCount = Random.Shared.Next(1, 5);
            for (var index = 0; index < callCount; index++)
            {
                _sut.AsyncDuplexStreamingCall(
                    new Method<object, object>(
                        MethodType.Unary,
                        _fixture.Create<string>(),
                        _fixture.Create<string>(),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                    ),
                    _fixture.Create<string>(),
                    new CallOptions());
            }

            Assert.Equal(0, _first.BlockingUnaryCalls);
            Assert.Equal(0, _first.AsyncUnaryCalls);
            Assert.Equal(0, _first.AsyncClientStreamingCalls);
            Assert.Equal(1, _first.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _first.AsyncServerStreamingCalls);
            
            Assert.Equal(0, _next.BlockingUnaryCalls);
            Assert.Equal(0, _next.AsyncUnaryCalls);
            Assert.Equal(0, _next.AsyncClientStreamingCalls);
            Assert.Equal(callCount - 1, _next.AsyncDuplexStreamingCalls);
            Assert.Equal(0, _next.AsyncServerStreamingCalls);
        }
        
        [Fact]
        public void AsyncServerStreamingCallReturnsExpectedResult()
        {
            var callCount = Random.Shared.Next(1, 5);
            for (var index = 0; index < callCount; index++)
            {
                _sut.AsyncServerStreamingCall(
                    new Method<object, object>(
                        MethodType.Unary,
                        _fixture.Create<string>(),
                        _fixture.Create<string>(),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object()),
                        new Marshaller<object>(_ => Array.Empty<byte>(), _ => new object())
                    ),
                    _fixture.Create<string>(),
                    new CallOptions(),
                    new object());
            }

            Assert.Equal(0, _first.BlockingUnaryCalls);
            Assert.Equal(0, _first.AsyncUnaryCalls);
            Assert.Equal(0, _first.AsyncClientStreamingCalls);
            Assert.Equal(0, _first.AsyncDuplexStreamingCalls);
            Assert.Equal(1, _first.AsyncServerStreamingCalls);
            
            Assert.Equal(0, _next.BlockingUnaryCalls);
            Assert.Equal(0, _next.AsyncUnaryCalls);
            Assert.Equal(0, _next.AsyncClientStreamingCalls);
            Assert.Equal(0, _next.AsyncDuplexStreamingCalls);
            Assert.Equal(callCount - 1, _next.AsyncServerStreamingCalls);
        }

        
        private class CountingCallInvoker : CallInvoker
        {
            public int BlockingUnaryCalls { get; private set; }
            
            public int AsyncUnaryCalls { get; private set; }
            
            public int AsyncServerStreamingCalls { get; private set; }
            public int AsyncClientStreamingCalls { get; private set; }
            public int AsyncDuplexStreamingCalls { get; private set; }
        
            public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                BlockingUnaryCalls++;
                return null!;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options, TRequest request)
            {
                AsyncUnaryCalls++;
                return null!;
            }

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options,
                TRequest request)
            {
                AsyncServerStreamingCalls++;
                return null!;
            }

            public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                AsyncClientStreamingCalls++;
                return null!;
            }

            public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string host, CallOptions options)
            {
                AsyncDuplexStreamingCalls++;
                return null!;
            }
        }
    }
}