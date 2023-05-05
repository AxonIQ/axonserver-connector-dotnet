using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AggregateEventStream : IAggregateEventStream
{
    private readonly AsyncServerStreamingCall<Event> _call;
    private readonly CancellationToken _channelCancellationToken;
    private readonly ILogger<AggregateEventStream> _logger;

    public AggregateEventStream(AsyncServerStreamingCall<Event> call, CancellationToken channelCancellationToken, ILogger<AggregateEventStream> logger)
    {
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _channelCancellationToken = channelCancellationToken;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAsyncEnumerator<Event> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(_call, CreateLinkedTokenSource(_channelCancellationToken, cancellationToken), _logger);
    }
    
    private static CancellationTokenSource? CreateLinkedTokenSource(CancellationToken channelCancellationToken, CancellationToken enumeratorCancellationToken)
    {
        return channelCancellationToken == CancellationToken.None && enumeratorCancellationToken == CancellationToken.None 
            ? null 
            : enumeratorCancellationToken == CancellationToken.None 
                ? CancellationTokenSource.CreateLinkedTokenSource(channelCancellationToken) 
                : CancellationTokenSource.CreateLinkedTokenSource(channelCancellationToken, enumeratorCancellationToken);
    }
    
    private class Enumerator : IAsyncEnumerator<Event>
    {
        private readonly AsyncServerStreamingCall<Event> _call;
        private readonly CancellationTokenSource? _cancellation;
        private readonly ILogger _logger;

        public Enumerator(AsyncServerStreamingCall<Event> call, CancellationTokenSource? cancellation, ILogger logger)
        {
            _call = call ?? throw new ArgumentNullException(nameof(call));
            _cancellation = cancellation;
            _logger = logger;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            return await _call.ResponseStream.MoveNext(_cancellation?.Token ?? CancellationToken.None);
        }

        public Event Current => _call.ResponseStream.Current;
        
        public ValueTask DisposeAsync()
        {
            _cancellation?.Dispose();
            _call.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public void Dispose()
    {
        _call.Dispose();
    }
}