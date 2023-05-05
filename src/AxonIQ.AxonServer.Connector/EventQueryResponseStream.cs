using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
internal class EventQueryResponseStream : IAsyncEnumerable<IEventQueryResultEntry>
{
    private static readonly PermitCount Initial = new(100);
    private static readonly PermitCount Threshold = new(25);
    private readonly Context _context;
    private readonly string _query;
    private readonly bool _liveStream;
    private readonly bool _querySnapshots;
    private readonly AsyncDuplexStreamingCall<QueryEventsRequest, QueryEventsResponse> _call;
    private readonly ILogger<EventQueryResponseStream> _logger;

    public EventQueryResponseStream(
        Context context, 
        string query, 
        bool liveStream, 
        bool querySnapshots,
        AsyncDuplexStreamingCall<QueryEventsRequest, QueryEventsResponse> call,
        ILogger<EventQueryResponseStream> logger)
    {
        _context = context;
        _query = query;
        _liveStream = liveStream;
        _querySnapshots = querySnapshots;
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public IAsyncEnumerator<IEventQueryResultEntry> GetAsyncEnumerator(CancellationToken cancellationToken = default)
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
            QuerySnapshots = _querySnapshots,
            ContextName = _context.ToString()
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
        var columns = ImmutableList<string>.Empty;
        while (await moved)
        {
            if (_call.ResponseStream.Current.DataCase == QueryEventsResponse.DataOneofCase.Columns)
            {
                columns = ImmutableList.CreateRange(_call.ResponseStream.Current.Columns.Column);
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