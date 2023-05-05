using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

internal class EventStream : IEventStream
{
    private readonly PermitCount _initial;
    private readonly PermitCount _threshold;
    private readonly bool _forceReadFromLeader;
    private readonly EventStreamToken _token;
    private readonly AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> _call;
    private readonly CancellationToken _channelCancellationToken;

    public EventStream(
        PermitCount initial, 
        PermitCount threshold,
        bool forceReadFromLeader,
        EventStreamToken token,
        AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> call,
        CancellationToken channelCancellationToken)
    {
        _initial = initial;
        _threshold = threshold;
        _forceReadFromLeader = forceReadFromLeader;
        _token = token;
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _channelCancellationToken = channelCancellationToken;
    }
    
    public IAsyncEnumerator<EventWithToken> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new Enumerator(
            _call,
            _forceReadFromLeader,
            _token,
            new FlowController(_initial, _threshold), 
            CreateLinkedTokenSource(_channelCancellationToken, cancellationToken));
    }

    private static CancellationTokenSource? CreateLinkedTokenSource(CancellationToken channelCancellationToken, CancellationToken enumeratorCancellationToken)
    {
        return channelCancellationToken == CancellationToken.None && enumeratorCancellationToken == CancellationToken.None 
            ? null 
            : enumeratorCancellationToken == CancellationToken.None 
                ? CancellationTokenSource.CreateLinkedTokenSource(channelCancellationToken) 
                : CancellationTokenSource.CreateLinkedTokenSource(channelCancellationToken, enumeratorCancellationToken);
    }

    public Task ExcludePayloadTypeAsync(string payloadType, string? revision)
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

    private class Enumerator : IAsyncEnumerator<EventWithToken>
    {
        private readonly AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> _call;
        private readonly bool _forceReadFromLeader;
        private readonly EventStreamToken _token;
        private readonly FlowController _controller;
        private readonly CancellationTokenSource? _cancellation;

        private const int NotInitiated = 0;
        private const int Initiated = 1;
        private int _initiated;

        public Enumerator(
            AsyncDuplexStreamingCall<GetEventsRequest, EventWithToken> call,
            bool forceReadFromLeader,
            EventStreamToken token,
            FlowController controller,
            CancellationTokenSource? cancellation)
        {
            _call = call ?? throw new ArgumentNullException(nameof(call));
            _forceReadFromLeader = forceReadFromLeader;
            _token = token;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _cancellation = cancellation;
            _initiated = NotInitiated;
        }
        
        public async ValueTask<bool> MoveNextAsync()
        {
            if (Interlocked.CompareExchange(ref _initiated, Initiated, NotInitiated) == NotInitiated)
            {
                await _call.RequestStream.WriteAsync(new GetEventsRequest
                {
                    ForceReadFromLeader = _forceReadFromLeader,
                    TrackingToken = _token.ToInt64() + 1L,
                    NumberOfPermits = _controller.Initial.ToInt64()
                });
            }
            var moved = _call.ResponseStream.MoveNext(_cancellation?.Token ?? CancellationToken.None);
            if (_controller.Increment())
            {
                await _call.RequestStream.WriteAsync(new GetEventsRequest
                {
                    NumberOfPermits = _controller.Threshold.ToInt64()
                });
            }

            return await moved;
        }

        public EventWithToken Current => _call.ResponseStream.Current;
        
        
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