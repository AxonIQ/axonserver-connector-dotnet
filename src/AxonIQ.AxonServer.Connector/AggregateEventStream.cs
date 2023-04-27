using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class AggregateEventStream : IAggregateEventStream
{
    private readonly AsyncServerStreamingCall<Event> _call;
    private readonly ILogger<AggregateEventStream> _logger;

    public AggregateEventStream(AsyncServerStreamingCall<Event> call,
        ILogger<AggregateEventStream> logger)
    {
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAsyncEnumerator<Event> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return _call.ResponseStream.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    public void Dispose()
    {
        _call.Dispose();
    }
}