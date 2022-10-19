using System.Threading.Channels;
using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Query;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class QuerySubscriptionResult : IQuerySubscriptionResult
{
    private readonly ClientIdentity _clientIdentity;
    private readonly QueryRequest _request;
    private readonly AsyncDuplexStreamingCall<SubscriptionQueryRequest, SubscriptionQueryResponse> _call;
    private readonly TaskCompletionSource<QueryResponse> _initialResultSource;
    private readonly Channel<QueryUpdate> _updateChannel;
    private readonly Task _consumer;
    private Task<QueryResponse>? _initialResult;
    private readonly ILogger<QuerySubscriptionResult> _logger;

    public QuerySubscriptionResult(
        ClientIdentity clientIdentity,
        QueryRequest request,
        PermitCount threshold,
        AsyncDuplexStreamingCall<SubscriptionQueryRequest, SubscriptionQueryResponse> call,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (call == null) throw new ArgumentNullException(nameof(call));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        
        _clientIdentity = clientIdentity;
        _request = request;
        _call = call;
        _logger = loggerFactory.CreateLogger<QuerySubscriptionResult>();
        _consumer = ConsumeResponseStream(_call.ResponseStream, ct);
        _initialResultSource = new TaskCompletionSource<QueryResponse>();
        _initialResult = null;
        _updateChannel = Channel.CreateUnbounded<QueryUpdate>(new UnboundedChannelOptions
            { SingleReader = false, SingleWriter = true, AllowSynchronousContinuations = false });
        Updates = new FlowControlAwareAsyncEnumerable<SubscriptionQueryRequest, QueryUpdate>(
            new FlowController(threshold, threshold),
            () =>
            new SubscriptionQueryRequest
            {
                FlowControl = new SubscriptionQuery
                {
                    NumberOfPermits = threshold.ToInt64()
                }
            }, 
            _call.RequestStream,
            _updateChannel.Reader.ReadAllAsync(ct),
            loggerFactory);
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<SubscriptionQueryResponse> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(cancellationToken: ct).ConfigureAwait(false))
            {
                switch (response.ResponseCase)
                {
                    case SubscriptionQueryResponse.ResponseOneofCase.InitialResult:
                        _logger.LogDebug("Received subscription query initial result. Subscription Id: {SubscriptionId}. Message Id: {MessageId}",
                            response.SubscriptionIdentifier,
                            response.MessageIdentifier);
                        _initialResultSource.TrySetResult(response.InitialResult);
                        break;
                    case SubscriptionQueryResponse.ResponseOneofCase.Update:
                        _logger.LogDebug("Received subscription query update. Subscription Id: {SubscriptionId}. Message Id: {MessageId}",
                            response.SubscriptionIdentifier,
                            response.MessageIdentifier);
                        await _updateChannel.Writer.WriteAsync(response.Update, ct).ConfigureAwait(false);
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
                        _initialResultSource.TrySetException(exception);
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

    private async Task<QueryResponse> GetInitialResultAsync()
    {
        if (Interlocked.CompareExchange(ref _initialResult, _initialResultSource.Task, null) == null)
        {
            await _call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                GetInitialResult = new SubscriptionQuery
                {
                    QueryRequest = _request,
                    SubscriptionIdentifier = _request.MessageIdentifier
                }
            }).ConfigureAwait(false);
        }

        return await _initialResult;
    }

    public Task<QueryResponse> InitialResult => GetInitialResultAsync();
    
    public IAsyncEnumerable<QueryUpdate> Updates { get; }
    
    public async ValueTask DisposeAsync()
    {
        _call.Dispose();
        await _consumer.ConfigureAwait(false);
        _initialResultSource.TrySetCanceled();
    }
}