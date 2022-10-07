using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class QueryChannel : IQueryChannel, IAsyncDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<QueryChannel> _logger;

    private State _state;
    private readonly Channel<Protocol> _channel;
    private readonly CancellationTokenSource _channelCancellation;
    private readonly Task _protocol;

    public QueryChannel(
        ClientIdentity clientIdentity,
        Context context,
        Func<DateTimeOffset> clock,
        CallInvoker callInvoker,
        PermitCount permits,
        PermitCount permitsBatch,
        ILoggerFactory loggerFactory)
    {
        if (clientIdentity == null) throw new ArgumentNullException(nameof(clientIdentity));
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        ClientIdentity = clientIdentity;
        Context = context;
        Clock = clock;
        PermitsBatch = permitsBatch;
        Service = new QueryService.QueryServiceClient(callInvoker);
        
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<QueryChannel>();

        _state = new State.Disconnected(
            new QuerySubscriptions(clientIdentity, clock),
            new FlowController(permits, permitsBatch),
            new Dictionary<string, QueryTask>(),
            new Dictionary<SubscriptionIdentifier, ISubscriptionQueryRegistration?>());
        _channel = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _channelCancellation = new CancellationTokenSource();
        _protocol = RunChannelProtocol(_channelCancellation.Token);
    }

    private async Task ConsumeResponseStream(IAsyncStreamReader<QueryProviderInbound> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var response in reader.ReadAllAsync(ct))
            {
                await _channel.Writer.WriteAsync(new Protocol.ReceiveQueryProviderInbound(response), ct);
            }
        }
        catch (ObjectDisposedException exception)
        {
            _logger.LogDebug(exception,
                "The query channel instruction stream is no longer being read because an object got disposed");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The query channel instruction stream is no longer being read because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "The query channel instruction stream is no longer being read because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "The query channel instruction stream is no longer being read because of an unexpected exception");
        }
    }

    private async Task EnsureConnected(CancellationToken ct)
    {
        switch (_state)
        {
            case State.Disconnected disconnected:
                try
                {
                    var stream = Service.OpenStream(cancellationToken: ct);
                    if (stream != null)
                    {
                        _logger.LogInformation(
                            "Opened query stream for context '{Context}'",
                            Context);

                        await stream.RequestStream.WriteAsync(new QueryProviderOutbound
                        {
                            FlowControl = new FlowControl
                            {
                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                Permits = disconnected.Flow.Initial.ToInt64()
                            }
                        }, ct);

                        disconnected.Flow.Reset();
                        
                        _state = new State.Connected(
                            stream,
                            ConsumeResponseStream(stream.ResponseStream, ct),
                            disconnected.QuerySubscriptions,
                            disconnected.Flow,
                            disconnected.QueryTasks,
                            disconnected.SubscriptionQueryRegistrations);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not open query stream for context '{Context}'",
                            Context);
                    }
                }
                catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                {
                    _logger.LogWarning(
                        "Could not open query stream for context '{Context}': no connection to AxonServer",
                        Context.ToString());
                }

                break;
            case State.Connected:
                _logger.LogDebug("QueryChannel for context '{Context}' is already connected",
                    Context.ToString());
                break;
        }
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                while (_channel.Reader.TryRead(out var message))
                {
                    _logger.LogDebug("Began {Message} when {State}", message.ToString(), _state.ToString());
                    switch (message)
                    {
                        case Protocol.Connect:
                            await EnsureConnected(ct);

                            break;
                        case Protocol.SubscribeQueryHandler subscribe:
                            await EnsureConnected(ct);
                            switch (_state)
                            {
                                case State.Connected connected:
                                    connected.QuerySubscriptions.RegisterQueryHandler(
                                        subscribe.QueryHandlerId,
                                        subscribe.CompletionSource,
                                        subscribe.Handler);
                                    
                                    foreach (var (subscriptionId, query) in subscribe.SubscribedQueries)
                                    {
                                        _logger.LogInformation(
                                            "Registered handler for query '{Query}' in context '{Context}'",
                                            query.QueryName.ToString(), Context.ToString());
                                    
                                        var instructionId = connected.QuerySubscriptions.SubscribeToQuery(
                                            subscriptionId,
                                            subscribe.QueryHandlerId,
                                            query);
                                        var request = new QueryProviderOutbound
                                        {
                                            InstructionId = instructionId.ToString(),
                                            Subscribe = new QuerySubscription
                                            {
                                                MessageId = instructionId.ToString(),
                                                Query = query.QueryName.ToString(),
                                                ResultName = query.ResultType,
                                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                ComponentName = ClientIdentity.ComponentName.ToString()
                                            }
                                        };
                                        await connected.Stream.RequestStream.WriteAsync(request, ct);
                                    }

                                    break;
                                case State.Disconnected:
                                    if (!subscribe.CompletionSource.Fault(
                                            new AxonServerException(
                                                ClientIdentity,
                                                ErrorCategory.Other,
                                                "Unable to subscribe queries and handler: no connection to AxonServer")))
                                    {
                                        _logger.LogWarning(
                                            "Could not fault the subscribe completion source of query handler '{QueryHandlerId}'",
                                            subscribe.QueryHandlerId.ToString());
                                    }

                                    break;
                            }

                            break;
                        case Protocol.UnsubscribeQueryHandler unsubscribe:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    connected.QuerySubscriptions.UnregisterQueryHandler(
                                        unsubscribe.QueryHandlerId,
                                        unsubscribe.CompletionSource);

                                    foreach (var subscribedQuery in unsubscribe.SubscribedQueries)
                                    {
                                        var instructionId = connected.QuerySubscriptions.UnsubscribeFromQuery(subscribedQuery.SubscriptionId);
                                        if (instructionId.HasValue)
                                        {
                                            _logger.LogInformation(
                                            "Unregistered handler for query '{QueryName}' in context '{Context}'",
                                            subscribedQuery.Query.QueryName.ToString(), Context.ToString());
                                            
                                            var request = new QueryProviderOutbound
                                            {
                                                InstructionId = instructionId.ToString(),
                                                Unsubscribe = new QuerySubscription
                                                {
                                                    MessageId = instructionId.ToString(),
                                                    Query = subscribedQuery.Query.QueryName.ToString(),
                                                    ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                    ComponentName = ClientIdentity.ComponentName.ToString()
                                                }
                                            };
                                            await connected.Stream.RequestStream.WriteAsync(request, ct);
                                        }
                                    }
                                    // subscriptions.UnregisterCommandHandler(
                                    //     unsubscribe.CommandHandlerId,
                                    //     unsubscribe.CompletionSource);
                                    //
                                    // foreach (var (subscriptionId, command) in unsubscribe.SubscribedCommands)
                                    // {
                                    //     var instructionId = subscriptions.UnsubscribeFromCommand(subscriptionId);
                                    //     if (instructionId.HasValue)
                                    //     {
                                    //         _logger.LogInformation(
                                    //             "Unregistered handler for command '{CommandName}' in context '{Context}'",
                                    //             command.ToString(), _context.ToString());
                                    //
                                    //         var request = new CommandProviderOutbound
                                    //         {
                                    //             InstructionId = instructionId.ToString(),
                                    //             Unsubscribe = new CommandSubscription
                                    //             {
                                    //                 MessageId = instructionId.ToString(),
                                    //                 Command = command.ToString(),
                                    //                 ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                    //                 ComponentName = ClientIdentity.ComponentName.ToString()
                                    //             }
                                    //         };
                                    //         await connected.Stream.RequestStream.WriteAsync(request);
                                    //     }
                                    // }

                                    break;
                                case State.Disconnected:
                                    if (!unsubscribe.CompletionSource.Fault(
                                            new AxonServerException(
                                                ClientIdentity,
                                                ErrorCategory.Other,
                                                "Unable to unsubscribe queries and handler: no connection to AxonServer")))
                                    {
                                        _logger.LogWarning(
                                            "Could not fault the unsubscribe completion source of command handler '{QueryHandlerId}'",
                                            unsubscribe.QueryHandlerId.ToString());
                                    }

                                    break;
                            }

                            break;
                        case Protocol.Reconnect:
                            switch (_state)
                            {
                                case State.Disconnected:
                                    break;
                            }

                            break;
                        case Protocol.Disconnect:
                            switch (_state)
                            {
                                case State.Disconnected:
                                    break;
                            }

                            break;
                        case Protocol.ReceiveQueryProviderInbound receive:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    switch (receive.Message.RequestCase)
                                    {
                                        case QueryProviderInbound.RequestOneofCase.None:
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.Ack:
                                            connected.QuerySubscriptions.Acknowledge(receive.Message.Ack);
                                            
                                            if (connected.Flow.Increment())
                                            {
                                                await connected.Stream.RequestStream.WriteAsync(new QueryProviderOutbound
                                                {
                                                    FlowControl = new FlowControl
                                                    {
                                                        ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                        Permits = connected.Flow.Threshold.ToInt64()
                                                    }
                                                }, ct);
                                            }
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.Query:
                                            {
                                                if (connected.QuerySubscriptions.ActiveHandlers.TryGetValue(
                                                        new QueryName(receive.Message.Query.Query), out var handlers))
                                                {
                                                    foreach (var handler in handlers)
                                                    {
                                                        connected.QueryTasks.Add(receive.Message.Query.MessageIdentifier,
                                                            new QueryTask(
                                                                handler.Handle(receive.Message.Query,
                                                                    new QueryResponseChannel(
                                                                        receive.Message.Query,
                                                                        instruction =>
                                                                            _channel.Writer.WriteAsync(
                                                                                new Protocol.SendQueryProviderOutbound(
                                                                                    instruction),
                                                                                ct)))));
                                                    }
                                                }
                                                else
                                                {
                                                    await connected.Stream.RequestStream.WriteAsync(new QueryProviderOutbound
                                                    {
                                                        QueryResponse = new QueryResponse
                                                        {
                                                            RequestIdentifier = receive.Message.Query.MessageIdentifier,
                                                            ErrorCode = ErrorCategory.NoHandlerForQuery.ToString(),
                                                            ErrorMessage = new ErrorMessage
                                                                { Message = "No handler for query" }
                                                        }
                                                    }, ct);
                                                }
                                            }
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.SubscriptionQueryRequest:
                                            switch(receive.Message.SubscriptionQueryRequest.RequestCase)
                                            {
                                                case SubscriptionQueryRequest.RequestOneofCase.None:
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.Subscribe:
                                                {
                                                    var subscribe = receive.Message.SubscriptionQueryRequest.Subscribe;
                                                    var subscriptionIdentifier =
                                                        new SubscriptionIdentifier(subscribe.SubscriptionIdentifier);
                                                    var query = new QueryName(subscribe.QueryRequest.Query);
                                                    if (connected.QuerySubscriptions.ActiveHandlers.TryGetValue(query,
                                                            out var handlers))
                                                    {
                                                        foreach (var handler in handlers)
                                                        {
                                                            connected.SubscriptionQueryRegistrations.Add(
                                                                subscriptionIdentifier,
                                                                handler.RegisterSubscriptionQuery(
                                                                    subscribe,
                                                                    new SubscriptionQueryUpdateResponseChannel(
                                                                        ClientIdentity,
                                                                        subscriptionIdentifier,
                                                                        instruction =>
                                                                            _channel.Writer.WriteAsync(
                                                                                new Protocol.SendQueryProviderOutbound(
                                                                                    instruction),
                                                                                ct))));
                                                        }
                                                    }

                                                    await connected.Stream.RequestStream.WriteAsync(
                                                        new QueryProviderOutbound
                                                        {
                                                            Ack = new InstructionAck
                                                            {
                                                                InstructionId = receive.Message.InstructionId,
                                                                Success = true
                                                            }
                                                        }, ct);
                                                }
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.Unsubscribe:
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.GetInitialResult:
                                                {
                                                    var getInitialResult = receive.Message.SubscriptionQueryRequest
                                                        .GetInitialResult;
                                                    var subscriptionIdentifier =
                                                        new SubscriptionIdentifier(getInitialResult
                                                            .SubscriptionIdentifier);
                                                    var responseChannel =
                                                        new SubscriptionQueryInitialResultResponseChannel(
                                                            subscriptionIdentifier,
                                                            getInitialResult.QueryRequest,
                                                            instruction =>
                                                                _channel.Writer.WriteAsync(
                                                                    new Protocol.SendQueryProviderOutbound(
                                                                        instruction),
                                                                    ct));
                                                    
                                                    if (connected.QuerySubscriptions.ActiveHandlers.TryGetValue(
                                                            new QueryName(getInitialResult.QueryRequest.Query), out var handlers))
                                                    {
                                                        foreach (var handler in handlers)
                                                        {
                                                            connected.QueryTasks.Add(
                                                                getInitialResult.QueryRequest.MessageIdentifier,
                                                                new QueryTask(handler.Handle(getInitialResult.QueryRequest, responseChannel))
                                                            );
                                                        }
                                                    }
                                                    else
                                                    {
                                                        await connected.Stream.RequestStream.WriteAsync(new QueryProviderOutbound
                                                        {
                                                            QueryResponse = new QueryResponse
                                                            {
                                                                RequestIdentifier = getInitialResult.QueryRequest.MessageIdentifier,
                                                                ErrorCode = ErrorCategory.NoHandlerForQuery.ToString(),
                                                                ErrorMessage = new ErrorMessage
                                                                    { Message = "No handler for query" }
                                                            }
                                                        }, ct);
                                                    }
                                                }
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.FlowControl:
                                                    break;
                                            }
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.QueryCancel:
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.QueryFlowControl:
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }


                                    break;
                            }

                            break;
                        case Protocol.SendQueryProviderOutbound send:
                            switch (_state)
                            {
                                case State.Connected connected:
                                    await connected.Stream.RequestStream.WriteAsync(send.Instruction, ct);
                                    break;
                                case State.Disconnected disconnected:
                                    break;
                            }

                            break;
                    }

                    _logger.LogDebug("Completed {Message} with {State}", message.ToString(), _state.ToString());
                }
            }
        }
        catch (RpcException exception) when (exception.Status.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogDebug(exception,
                "Query channel protocol loop is exciting because an rpc call was cancelled");
        }
        catch (TaskCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Query channel protocol loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogDebug(exception,
                "Query channel protocol loop is exciting because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Query channel protocol loop is exciting because of an unexpected exception");
        }
    }
    
    public ClientIdentity ClientIdentity { get; }
    public Context Context { get; }
    public Func<DateTimeOffset> Clock { get; }
    public PermitCount PermitsBatch { get; }
    public QueryService.QueryServiceClient Service { get; }
    
    private record Protocol
    {
        public record Connect : Protocol;

        public record ReceiveQueryProviderInbound(QueryProviderInbound Message) : Protocol;

        public record Reconnect : Protocol;

        public record Disconnect : Protocol;
        
        public record SubscribeQueryHandler(
            QueryHandlerId QueryHandlerId,
            IQueryHandler Handler,
            SubscribedQuery[] SubscribedQueries,
            CountdownCompletionSource CompletionSource) : Protocol;
        
        public record UnsubscribeQueryHandler(
            QueryHandlerId QueryHandlerId,
            SubscribedQuery[] SubscribedQueries,
            CountdownCompletionSource CompletionSource) : Protocol;

        public record SendQueryProviderOutbound(QueryProviderOutbound Instruction) : Protocol;
    }
    
    private record SubscribedQuery(SubscriptionId SubscriptionId, QueryDefinition Query);
    
    private record State
    {
        public record Disconnected(
            QuerySubscriptions QuerySubscriptions,
            FlowController Flow,
            Dictionary<string, QueryTask> QueryTasks,
            Dictionary<SubscriptionIdentifier, ISubscriptionQueryRegistration?> SubscriptionQueryRegistrations) : State;

        public record Connected(
            AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound> Stream,
            Task ConsumeResponseStreamLoop,
            QuerySubscriptions QuerySubscriptions,
            FlowController Flow,
            Dictionary<string, QueryTask> QueryTasks,
            Dictionary<SubscriptionIdentifier, ISubscriptionQueryRegistration?> SubscriptionQueryRegistrations) : State;
    }
    
    public async Task<IQueryHandlerRegistration> RegisterQueryHandler(IQueryHandler handler, params QueryDefinition[] queries)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (queries == null) throw new ArgumentNullException(nameof(queries));
        if (queries.Length == 0)
            throw new ArgumentException("The queries requires at least one query to be specified",
                nameof(queries));

        var queryHandlerId = QueryHandlerId.New();
        var subscribedQueries = queries.Select(query => new SubscribedQuery(SubscriptionId.New(), query)).ToArray();
        var subscribeCompletionSource = new CountdownCompletionSource(queries.Length);
        await _channel.Writer.WriteAsync(new Protocol.SubscribeQueryHandler(
            queryHandlerId, 
            handler,
            subscribedQueries, 
            subscribeCompletionSource));
        return new QueryHandlerRegistration(subscribeCompletionSource.Completion, async () =>
        {
            var unsubscribeCompletionSource = new CountdownCompletionSource(queries.Length);
            await _channel.Writer.WriteAsync(
                new Protocol.UnsubscribeQueryHandler(
                    queryHandlerId,
                    subscribedQueries,
                    unsubscribeCompletionSource));
            await unsubscribeCompletionSource.Completion;
        });
    }

    public IAsyncEnumerable<QueryResponse> Query(QueryRequest query, CancellationToken ct)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        var request = new QueryRequest(query);
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = Guid.NewGuid().ToString("D");
        }
        try
        {
            var stream = Service.Query(query, cancellationToken: ct);
            return new QueryResponseConsumerStream(stream.ResponseStream.ReadAllAsync(ct), stream);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                ClientIdentity,
                ErrorCategory.QueryDispatchError,
                "An error occurred while attempting to dispatch a query",
                exception);
        }
    }

    public async Task<IQuerySubscriptionResult> SubscriptionQuery(QueryRequest query, SerializedObject updateType, PermitCount bufferSize, PermitCount fetchSize, CancellationToken ct)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (updateType == null) throw new ArgumentNullException(nameof(updateType));
        var request = new QueryRequest(query);
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = Guid.NewGuid().ToString("D");
        }
        try
        {
            var call = Service.Subscription(cancellationToken: ct);
            await call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                Subscribe = new SubscriptionQuery
                {
                    QueryRequest = request,
                    SubscriptionIdentifier = request.MessageIdentifier,
                    UpdateResponseType = updateType
                }
            }, ct);
            await call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                FlowControl = new SubscriptionQuery
                {
                    NumberOfPermits = bufferSize.ToInt64()
                }
            }, ct);

            return new QuerySubscriptionResult(
                ClientIdentity, 
                request,
                PermitsBatch, 
                call,
                _loggerFactory,
                ct);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                ClientIdentity,
                ErrorCategory.QueryDispatchError,
                "An error occurred while attempting to dispatch a query",
                exception);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        _channelCancellation.Cancel();
        _channel.Writer.Complete();
        await _channel.Reader.Completion;
        await _protocol;
        _channelCancellation.Dispose();
        _protocol.Dispose();
    }
}