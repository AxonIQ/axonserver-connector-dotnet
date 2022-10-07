using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class FlowControlAwareAsyncEnumerator<TRequest, TResponse> : IAsyncEnumerator<TResponse>
{
    private readonly Func<TRequest> _builder;
    private readonly IAsyncStreamWriter<TRequest> _writer;
    private readonly IAsyncEnumerator<TResponse> _enumerator;
    private readonly ILogger<FlowControlAwareAsyncEnumerator<TRequest, TResponse>> _logger;
    private readonly FlowController _controller;

    public FlowControlAwareAsyncEnumerator(
        FlowController controller, 
        Func<TRequest> flowControlRequestBuilder,
        IAsyncStreamWriter<TRequest> writer,
        IAsyncEnumerator<TResponse> enumerator, 
        ILogger<FlowControlAwareAsyncEnumerator<TRequest, TResponse>> logger)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _builder = flowControlRequestBuilder ?? throw new ArgumentNullException(nameof(flowControlRequestBuilder));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        var move = _enumerator.MoveNextAsync();
        if (_controller.Increment())
        {
            var request = _builder();
            _logger.LogDebug("Sending request for data {Request}", request);
            await _writer.WriteAsync(request);
        }
        return await move;
    }

    public TResponse Current => _enumerator.Current;
    
    public ValueTask DisposeAsync()
    {
        return _enumerator.DisposeAsync();
    }
}