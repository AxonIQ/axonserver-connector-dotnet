using System.Threading.Channels;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class QuerySubscriptionResult : IQuerySubscriptionResult
{
    private readonly ClientIdentity _clientIdentity;
    private readonly AsyncDuplexStreamingCall<SubscriptionQueryRequest, SubscriptionQueryResponse> _call;
    private readonly ILogger<QuerySubscriptionResult> _logger;
    private readonly TaskCompletionSource<QueryResponse> _initialResult;
    private readonly Channel<QueryUpdate> _updateChannel;
    private readonly Task _consumer;

    public QuerySubscriptionResult(ClientIdentity clientIdentity, PermitCount threshold,
        AsyncDuplexStreamingCall<SubscriptionQueryRequest, SubscriptionQueryResponse> call,
        ILogger<QuerySubscriptionResult> logger, CancellationToken ct)
    {
        _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumer = ConsumeResponseStream(_call.ResponseStream, ct);
        _initialResult = new TaskCompletionSource<QueryResponse>();
        _updateChannel = Channel.CreateUnbounded<QueryUpdate>(new UnboundedChannelOptions
            { SingleReader = false, SingleWriter = true, AllowSynchronousContinuations = false });
        Updates = new FlowControlAwareAsyncEnumerable(threshold, _call.RequestStream, _updateChannel.Reader.ReadAllAsync(ct));
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<SubscriptionQueryResponse> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(cancellationToken: ct))
            {
                switch (response.ResponseCase)
                {
                    case SubscriptionQueryResponse.ResponseOneofCase.InitialResult:
                        _logger.LogDebug("Received subscription query initial result. Subscription Id: {SubscriptionId}. Message Id: {MessageId}",
                            response.SubscriptionIdentifier,
                            response.MessageIdentifier);
                        _initialResult.TrySetResult(response.InitialResult);
                        break;
                    case SubscriptionQueryResponse.ResponseOneofCase.Update:
                        _logger.LogDebug("Received subscription query update. Subscription Id: {SubscriptionId}. Message Id: {MessageId}",
                            response.SubscriptionIdentifier,
                            response.MessageIdentifier);
                        await _updateChannel.Writer.WriteAsync(response.Update, ct);
                        break;
                    case SubscriptionQueryResponse.ResponseOneofCase.Complete:
                        _logger.LogDebug("Received subscription query complete. Subscription Id: {SubscriptionId}",
                            response.SubscriptionIdentifier);
                        _updateChannel.Writer.Complete();
                        break;
                    case SubscriptionQueryResponse.ResponseOneofCase.CompleteExceptionally:
                        _logger.LogDebug("Received subscription query complete exceptionally. Subscription Id: {SubscriptionId}",
                            response.SubscriptionIdentifier);
                        var exception = new AxonServerException(
                            _clientIdentity,
                            ErrorCategory.Parse(response.CompleteExceptionally.ErrorCode),
                            response.CompleteExceptionally.ErrorMessage.Message
                        );
                        _updateChannel.Writer.Complete(exception);
                        _initialResult.TrySetException(exception);
                        break;
                    default:
                        _logger.LogInformation("Received unsupported message from subscription query. It doesn't declare one of the expected types");
                        break;
                }
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception,
                "The query subscription result stream is no longer being read because an object got disposed");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The query subscription result stream is no longer being read because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The query subscription result stream is no longer being read because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "The query subscription result stream is no longer being read because of an unexpected exception");
        }
    }

    public Task<QueryResponse> InitialResult => _initialResult.Task;

    // if (Interlocked.CompareExchange(ref _initialResult, _initialResultSource.Task, null) == null)
    // {
    //     await _call.RequestStream.WriteAsync(new SubscriptionQueryRequest
    //     {
    //         Subscribe = new SubscriptionQuery
    //         {
    //             QueryRequest = request,
    //             SubscriptionIdentifier = request.MessageIdentifier,
    //             UpdateResponseType = updateType
    //         }
    //     });
    // }
    //
    // return await _initialResult;
    public IAsyncEnumerable<QueryUpdate> Updates { get; }
    
    public async ValueTask DisposeAsync()
    {
        _call.Dispose();
        await _consumer;
        _initialResult.TrySetCanceled();
    }

    private class FlowControlAwareAsyncEnumerable : IAsyncEnumerable<QueryUpdate>
    {
        private readonly PermitCount _threshold;
        private readonly IAsyncStreamWriter<SubscriptionQueryRequest> _writer;
        private readonly IAsyncEnumerable<QueryUpdate> _enumerable;

        public FlowControlAwareAsyncEnumerable(PermitCount threshold, IAsyncStreamWriter<SubscriptionQueryRequest> writer, IAsyncEnumerable<QueryUpdate> enumerable)
        {
            _threshold = threshold;
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        }

        public IAsyncEnumerator<QueryUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = new())
        {
            return new FlowControlAwareAsyncEnumerator(_threshold, _writer, _enumerable.GetAsyncEnumerator(cancellationToken));
        }
    }
    
    private class FlowControlAwareAsyncEnumerator : IAsyncEnumerator<QueryUpdate>
    {
        private readonly IAsyncStreamWriter<SubscriptionQueryRequest> _writer;
        private readonly IAsyncEnumerator<QueryUpdate> _enumerator;
        private readonly FlowController _controller;

        public FlowControlAwareAsyncEnumerator(PermitCount threshold, IAsyncStreamWriter<SubscriptionQueryRequest> writer, IAsyncEnumerator<QueryUpdate> enumerator)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _controller = new FlowController(threshold);
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            var moved = await _enumerator.MoveNextAsync();
            if (moved && _controller.Increment())
            {
                await _writer.WriteAsync(new SubscriptionQueryRequest
                {
                    FlowControl = new SubscriptionQuery
                    {
                        NumberOfPermits = _controller.Threshold.ToInt64()
                    }
                });
            }
            return moved;
        }

        public QueryUpdate Current => _enumerator.Current;
        
        public ValueTask DisposeAsync()
        {
            return _enumerator.DisposeAsync();
        }
    }
}