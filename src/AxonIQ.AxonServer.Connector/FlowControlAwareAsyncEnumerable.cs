using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class FlowControlAwareAsyncEnumerable<TRequest, TResponse> : IAsyncEnumerable<TResponse>
{
    private readonly FlowController _controller;
    private readonly Func<TRequest> _builder;
    private readonly IAsyncStreamWriter<TRequest> _writer;
    private readonly IAsyncEnumerable<TResponse> _enumerable;
    private readonly ILoggerFactory _loggerFactory;

    public FlowControlAwareAsyncEnumerable(
        FlowController controller,
        Func<TRequest> flowControlRequestBuilder,
        IAsyncStreamWriter<TRequest> writer, 
        IAsyncEnumerable<TResponse> enumerable,
        ILoggerFactory loggerFactory)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _builder = flowControlRequestBuilder ?? throw new ArgumentNullException(nameof(flowControlRequestBuilder));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IAsyncEnumerator<TResponse> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new FlowControlAwareAsyncEnumerator<TRequest, TResponse>(
            _controller,
            _builder,
            _writer, 
            _enumerable.GetAsyncEnumerator(cancellationToken), 
            _loggerFactory.CreateLogger<FlowControlAwareAsyncEnumerator<TRequest, TResponse>>());
    }
}