using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
public class EventQueryResponseStream : IAsyncEnumerable<IEventQueryResultEntry>
{
    private static readonly PermitCount Initial = new(100);
    private static readonly PermitCount Threshold = new(25);
    private readonly string _query;
    private readonly bool _liveStream;
    private readonly bool _querySnapshots;
    private readonly AsyncDuplexStreamingCall<QueryEventsRequest, QueryEventsResponse> _call;
    private readonly ILogger<EventQueryResponseStream> _logger;

    internal EventQueryResponseStream(string query, bool liveStream, bool querySnapshots, AsyncDuplexStreamingCall<QueryEventsRequest, QueryEventsResponse> call, ILogger<EventQueryResponseStream> logger)
    {
        _query = query;
        _liveStream = liveStream;
        _querySnapshots = querySnapshots;
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public IAsyncEnumerator<IEventQueryResultEntry> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        return ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }
    
    private async IAsyncEnumerable<IEventQueryResultEntry> ReadAllAsync([EnumeratorCancellation]CancellationToken cancellationToken)
    {
        var controller = new FlowController(Initial, Threshold);
        // Initial Flow Control message
        await _call.RequestStream.WriteAsync(new QueryEventsRequest
        {
            Query = _query,
            LiveEvents = _liveStream,
            NumberOfPermits = Initial.ToInt64(),
            QuerySnapshots = _querySnapshots
        }).ConfigureAwait(false);
        var moved = _call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
        // Flow Control message
        if (controller.Increment())
        {
            await _call.RequestStream.WriteAsync(new QueryEventsRequest
            {
                NumberOfPermits = Threshold.ToInt64()
            }).ConfigureAwait(false);
        }
        var columns = new List<string>();
        while (await moved)
        {
            if (_call.ResponseStream.Current.DataCase == QueryEventsResponse.DataOneofCase.Columns)
            {
                // This could result in issues if the caller holds onto QueryEventsResponseToEventQueryResultEntryAdapter
                // which in turn reuses this columns instance.
                columns.Clear();  
                columns.AddRange(_call.ResponseStream.Current.Columns.Column);
            }
            else if (_call.ResponseStream.Current.DataCase == QueryEventsResponse.DataOneofCase.Row)
            {
                yield return new QueryEventsResponseToEventQueryResultEntryAdapter(_call.ResponseStream.Current, columns);
            }
            moved = _call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
            // Flow Control message
            if (controller.Increment())
            {
                await _call.RequestStream.WriteAsync(new QueryEventsRequest
                {
                    NumberOfPermits = Threshold.ToInt64()
                }).ConfigureAwait(false);
            }
        }
    }
}