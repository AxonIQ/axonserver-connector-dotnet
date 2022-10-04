using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class EventStream : IEventStream
{
    private readonly PermitCount _initial;
    private readonly PermitCount _threshold;
    private readonly AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> _call;
    private readonly ILoggerFactory _loggerFactory;

    public EventStream(PermitCount initial, PermitCount threshold, AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> call, ILoggerFactory loggerFactory)
    {
        _initial = initial;
        _threshold = threshold;
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }
    
    public IAsyncEnumerator<EventWithToken> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return new FlowControlAwareAsyncEnumerable<GetEventsRequest, EventWithToken>(
                new FlowController(_initial, _threshold),
                () =>
                    new GetEventsRequest
                    {
                        NumberOfPermits = _threshold.ToInt64()
                    },
                _call.RequestStream,
                _call.ResponseStream.ReadAllAsync(cancellationToken),
                _loggerFactory)
            .GetAsyncEnumerator(cancellationToken);
    }

    public Task ExcludePayloadType(string payloadType, string? revision)
    {
        if (payloadType == null) throw new ArgumentNullException(nameof(payloadType));
        
        var request = new GetEventsRequest();
        request.Blacklist.Add(new PayloadDescription
        {
            Type = payloadType,
            Revision = revision ?? "" 
        });
        return _call.RequestStream.WriteAsync(request);
    }

    public void Dispose()
    {
        _call.Dispose();
    }
}