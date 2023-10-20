using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
[SuppressMessage("Reliability", "CA2016:Forward the \'CancellationToken\' parameter to methods")]
internal class QueryChannel : IQueryChannel, IAsyncDisposable
{
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromSeconds(15);
    
    private readonly IOwnerAxonServerConnection _connection;
    private readonly TimeSpan _purgeInterval;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<QueryChannel> _logger;

    private readonly AxonPriorityActor<Message, State> _actor;

    public QueryChannel(
        IOwnerAxonServerConnection connection,
        IScheduler scheduler,
        PermitCount permits,
        PermitCount permitsBatch,
        ILoggerFactory loggerFactory) : this(connection, scheduler, permits, permitsBatch, PurgeInterval, loggerFactory)
    {
    }

    public QueryChannel(
        IOwnerAxonServerConnection connection,
        IScheduler scheduler,
        PermitCount permits,
        PermitCount permitsBatch,
        TimeSpan purgeInterval,
        ILoggerFactory loggerFactory)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

        _connection = connection;
        _purgeInterval = purgeInterval;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<QueryChannel>();
        
        PermitsBatch = permitsBatch;
        Service = new QueryService.QueryServiceClient(connection.CallInvoker);

        _actor = new AxonPriorityActor<Message, State>(
            Receive,
            new State.Disconnected(
                new QueryHandlerCollection(connection.ClientIdentity, scheduler.Clock), 
                new FlowController(permits, permitsBatch), 
                new QueryExecutions(),
                new SubscriptionQueryExecutions()),
            scheduler,
            _logger);
    }
    
// ReSharper disable MethodSupportsCancellation
#pragma warning disable CS4014
#pragma warning disable CA2016
    private async Task<State> Receive(Message message, State state, CancellationToken ct)
    {
        switch (state, message)
        {
            case (State.Disconnected disconnected, Message.Connect):
                await _actor.TellAsync(new Message.OpenStream(), ct).ConfigureAwait(false);
                state = new State.Connecting(
                    new List<Message>(), 
                    disconnected.QueryHandlers, 
                    disconnected.Flow, 
                    disconnected.QueryExecutions,
                    disconnected.SubscriptionQueryExecutions);
                break;
            case (State.Connecting, Message.OpenStream):
                await _actor.TellToAsync(
                    () => Service.OpenStream(cancellationToken: ct),
                    result => new Message.StreamOpened(result),
                    ct);
                break;
            case (State.Connecting connecting, Message.StreamOpened opened):
                switch (opened.Result)
                {
                    case TaskResult<AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound>>.Ok ok:
                        _logger.LogInformation(
                            "Opened query stream for context '{Context}'",
                            _connection.Context);
                        
                        var consumerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        var consumer =
                            ok
                                .Value
                                .ResponseStream
                                .TellToAsync(
                                    _actor,
                                    result => new Message.ReceiveQueryProviderInbound(result),
                                    _logger,
                                    consumerCancellationTokenSource.Token);
                        
                        connecting.Flow.Reset();

                        await _actor.TellAsync(
                            new Message[]
                            {
                                new Message.SendQueryProviderOutbound(new QueryProviderOutbound
                                {
                                    FlowControl = new FlowControl
                                    {
                                        ClientId = _connection.ClientIdentity.ClientInstanceId.ToString(),
                                        Permits = connecting.Flow.Initial.ToInt64()
                                    }
                                }),
                                new Message.OnConnected()
                            }, ct).ConfigureAwait(false);
                        
                        await _actor.TellAsync(connecting.DeferredMessages);

                        await _actor.TellAsync(
                            connecting
                                .QueryHandlers
                                .BeginSubscribeToAllInstructions()
                                .Select(instruction => new Message.SendQueryProviderOutbound(instruction))
                                .ToArray(), ct).ConfigureAwait(false);
                        
                        if (connecting.QueryHandlers.HasRegisteredQueries)
                        {
                            await _actor.ScheduleAsync(new Message.PurgeOverdueInstructions(), _purgeInterval, ct).ConfigureAwait(false);
                        }
                        
                        state = new State.Connected(
                            ok.Value,
                            consumer,
                            consumerCancellationTokenSource,
                            new OngoingQueryCollection(),
                            connecting.QueryHandlers, 
                            connecting.Flow,
                            connecting.QueryExecutions,
                            connecting.SubscriptionQueryExecutions);
                        
                        break;
                    case TaskResult<AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound>>.Error error:
                        var due = ScheduleDue.FromException(error.Exception);
                        
                        _logger.LogError(error.Exception, "Failed to open query stream. Retrying in {Due}ms", due.TotalMilliseconds);
                        
                        await _actor.ScheduleAsync(new Message.OpenStream(), due, ct).ConfigureAwait(false);
                        
                        break;
                }
                break;
            case (State.Connected connected, Message.ReceiveQueryProviderInbound receive):
                switch (receive.Result)
                {
                    case TaskResult<QueryProviderInbound>.Ok ok:
                        if (InstructionId.TryParse(ok.Value.InstructionId, out var instructionId))
                        {
                            await _actor.TellAsync(new Message.SendQueryProviderOutbound(new QueryProviderOutbound
                            {
                                Ack = new InstructionAck
                                {
                                    InstructionId = instructionId.ToString(),
                                    Success = true
                                }
                            }), ct);
                        }
                        
                        switch (ok.Value.RequestCase)
                        {
                            case QueryProviderInbound.RequestOneofCase.Ack:
                                if (connected.QueryHandlers.TryCompleteSubscribeToQueryInstruction(ok.Value.Ack))
                                {
                                    _logger.LogDebug("Subscribe to query instruction '{InstructionId}' got acknowledged", ok.Value.Ack.InstructionId);
                                } 
                                else if (connected.QueryHandlers.TryCompleteUnsubscribeFromQueryInstruction(ok.Value.Ack))
                                {
                                    _logger.LogDebug("Unsubscribe from query instruction '{InstructionId}' got acknowledged", ok.Value.Ack.InstructionId);
                                }
                                else
                                {
                                    _logger.LogWarning("Unknown instruction '{InstructionId}' got acknowledged", ok.Value.Ack.InstructionId);
                                }
                                break;
                            case QueryProviderInbound.RequestOneofCase.Query:
                            {
                                var query = ok.Value.Query;
                                var name = new QueryName(query.Query);
                                var handlers = connected.QueryHandlers.ResolveQueryHandlers(name);
                                switch (handlers.Count)
                                {
                                    case 0:
                                        await _actor.TellAsync(new Message.SendQueryProviderOutbound(
                                            new QueryProviderOutbound
                                            {
                                                QueryResponse = new QueryResponse
                                                {
                                                    RequestIdentifier =
                                                        query.MessageIdentifier,
                                                    ErrorCode = ErrorCategory.NoHandlerForQuery.ToString(),
                                                    ErrorMessage = new ErrorMessage
                                                        { Message = "No handler for query" }
                                                }
                                            }), ct);
                                        break;
                                    default:
                                    {
                                        var translator = QueryReplyTranslation.ForQuery(query);
                                        var requestId = InstructionId.Parse(query.MessageIdentifier);
                                        var flowControl = new ConcurrentFlowControl();
                                        var flowControlledChannel = new FlowControlledChannel<QueryReply>(flowControl);
                                        var cancellationTokenSource =
                                            CancellationTokenSource.CreateLinkedTokenSource(ct);
                                        var buffers = new List<Channel<QueryReply>>(handlers.Count);
                                        foreach (var handler in handlers)
                                        {
                                            var buffer = Channels.CreateBounded<QueryReply>(32);
                                            var responseChannel = new BufferedQueryResponseChannel(buffer);
                                            Task.Run(
                                                    () => handler.HandleAsync(query, responseChannel,
                                                        cancellationTokenSource.Token), ct)
                                                .TellToAsync(
                                                    _actor,
                                                    result => new Message.QueryHandlerCompleted(requestId, result),
                                                    ct);
                                            buffers.Add(buffer);
                                        }

                                        flowControlledChannel
                                            .PipeFromAll(buffers, ct)
                                            .TellToAsync(
                                                _actor,
                                                result => new Message.QueryReplyForwardingCompleted(requestId, result),
                                                ct);

                                        flowControlledChannel
                                            .TellQueryRepliesToAsync(
                                                _actor,
                                                reply => translator(reply).Select(instruction =>
                                                    new Message.SendQueryProviderOutbound(instruction)),
                                                ct);
                                        
                                        if (!query.SupportsStreaming())
                                        {
                                            flowControl.Request(int.MaxValue);    
                                        }

                                        state.QueryExecutions.Add(requestId,
                                            new QueryExecution(requestId, flowControl, cancellationTokenSource));
                                    }
                                        break;
                                }
                            }
                                break;
                            case QueryProviderInbound.RequestOneofCase.SubscriptionQueryRequest:
                            {
                                var subscriptionQueryRequest = ok.Value.SubscriptionQueryRequest;
                                switch (subscriptionQueryRequest.RequestCase)
                                {
                                    case SubscriptionQueryRequest.RequestOneofCase.Subscribe:
                                    {
                                        var id = new SubscriptionId(
                                            subscriptionQueryRequest.Subscribe.SubscriptionIdentifier);
                                        var name = new QueryName(subscriptionQueryRequest.Subscribe.QueryRequest.Query);
                                        var handlers = connected.QueryHandlers.ResolveQueryHandlers(name);
                                        var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                        foreach (var handler in handlers)
                                        {
                                            Task.Run(async () =>
                                            {
                                                var handle =
                                                    handler
                                                        .TryHandleAsync(
                                                            subscriptionQueryRequest.Subscribe,
                                                            new SubscriptionQueryUpdateResponseChannel(
                                                                _connection.ClientIdentity,
                                                                id,
                                                                instruction =>
                                                                    _actor.TellAsync(
                                                                        new Message.SendQueryProviderOutbound(
                                                                            instruction),
                                                                        cancellationTokenSource.Token)),
                                                            cancellationTokenSource.Token);
                                                if (handle != null)
                                                {
                                                    await handle
                                                        .TellToAsync(_actor,
                                                        result => new Message.SubscriptionQueryHandlerCompleted(id, result),
                                                        ct);
                                                }
                                            }, cancellationTokenSource.Token);
                                        }
                                        connected.SubscriptionQueryExecutions.Add(id, new SubscriptionQueryExecution(id, cancellationTokenSource));
                                    }
                                        break;
                                    case SubscriptionQueryRequest.RequestOneofCase.Unsubscribe:
                                    {
                                        var id = new SubscriptionId(
                                            subscriptionQueryRequest.Unsubscribe.SubscriptionIdentifier);
                                        if (connected.SubscriptionQueryExecutions.TryRemove(id, out var execution)
                                            && execution != null)
                                        {
                                            execution.CancellationTokenSource.Cancel();
                                            execution.CancellationTokenSource.Dispose();
                                        }
                                    }
                                        break;
                                    case SubscriptionQueryRequest.RequestOneofCase.GetInitialResult:
                                    {
                                        var getInitialResult = subscriptionQueryRequest.GetInitialResult;
                                        var query = getInitialResult.QueryRequest;
                                        var name = new QueryName(query.Query);
                                        var handlers = connected.QueryHandlers.ResolveQueryHandlers(name);
                                        switch (handlers.Count)
                                        {
                                            case 0:
                                                await _actor.TellAsync(new Message.SendQueryProviderOutbound(
                                                    new QueryProviderOutbound
                                                    {
                                                        QueryResponse = new QueryResponse
                                                        {
                                                            RequestIdentifier =
                                                                query.MessageIdentifier,
                                                            ErrorCode = ErrorCategory.NoHandlerForQuery.ToString(),
                                                            ErrorMessage = new ErrorMessage
                                                                { Message = "No handler for query" }
                                                        }
                                                    }), ct);
                                                break;
                                            default:
                                            {
                                                var translator = QueryReplyTranslation.ForSubscriptionQuery(getInitialResult);
                                                var requestId = InstructionId.Parse(query.MessageIdentifier);
                                                var flowControl = new ConcurrentFlowControl();
                                                var flowControlledChannel =
                                                    new FlowControlledChannel<QueryReply>(flowControl);
                                                var cancellationTokenSource =
                                                    CancellationTokenSource.CreateLinkedTokenSource(ct);
                                                var buffers = new List<Channel<QueryReply>>(handlers.Count);
                                                foreach (var handler in handlers)
                                                {
                                                    var buffer = Channels.CreateBounded<QueryReply>(32);
                                                    var responseChannel = new BufferedQueryResponseChannel(buffer);
                                                    Task.Run(
                                                            () => handler.HandleAsync(query, responseChannel,
                                                                cancellationTokenSource.Token), ct)
                                                        .TellToAsync(
                                                            _actor,
                                                            result => new Message.QueryHandlerCompleted(requestId,
                                                                result),
                                                            ct);
                                                    buffers.Add(buffer);
                                                }

                                                flowControlledChannel
                                                    .PipeFromAll(buffers, ct)
                                                    .TellToAsync(
                                                        _actor,
                                                        result => new Message.QueryReplyForwardingCompleted(requestId,
                                                            result),
                                                        ct);

                                                flowControlledChannel
                                                    .TellQueryRepliesToAsync(
                                                        _actor,
                                                        reply => translator(reply).Select(instruction =>
                                                            new Message.SendQueryProviderOutbound(instruction)),
                                                        ct);

                                                if (!query.SupportsStreaming())
                                                {
                                                    flowControl.Request(int.MaxValue);    
                                                }
                                                
                                                state.QueryExecutions.Add(
                                                    requestId,
                                                    new QueryExecution(
                                                        requestId, 
                                                        flowControl,
                                                        cancellationTokenSource));
                                            }
                                                break;
                                        }

                                        break;
                                    }
                                }
                            }
                                break;
                            case QueryProviderInbound.RequestOneofCase.QueryCancel:
                            {
                                if (InstructionId.TryParse(ok.Value.QueryCancel.RequestId,
                                        out var queryId))
                                {
                                    if (connected.QueryExecutions.TryGet(queryId, out var execution) && execution != null)
                                    {
                                        execution.FlowControl.Cancel();
                                        execution.CancellationTokenSource.Cancel();
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Could not find query with id '{QueryId}' to cancel",
                                            queryId);
                                    }
                                }
                            }
                                break;
                            case QueryProviderInbound.RequestOneofCase.QueryFlowControl:
                            {
                                if (InstructionId.TryParse(ok.Value.QueryFlowControl.QueryReference.RequestId,
                                        out var queryId))
                                {
                                    if (connected.QueryExecutions.TryGet(queryId, out var execution) && execution != null)
                                    {
                                        execution.FlowControl.Request(ok.Value.QueryFlowControl.Permits);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Could not find query with id '{QueryId}' to flow control",
                                            queryId);
                                    }
                                }
                            }
                                break;
                        }
                        
                        if (connected.Flow.Increment())
                        {
                            await _actor.TellAsync(new Message.SendQueryProviderOutbound(new QueryProviderOutbound
                            {
                                FlowControl = new FlowControl
                                {
                                    ClientId = _connection.ClientIdentity.ClientInstanceId.ToString(),
                                    Permits = connected.Flow.Threshold.ToInt64()
                                }
                            }), ct);
                        }

                        break;
                    case TaskResult<QueryProviderInbound>.Error:
                        try
                        {
                            connected.ConsumeQueryProviderInboundInstructionsCancellationTokenSource.Cancel();
                            await connected.ConsumeQueryProviderInboundInstructions.ConfigureAwait(false);
                            connected.Call.Dispose();
                            connected.ConsumeQueryProviderInboundInstructionsCancellationTokenSource.Dispose();
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Failed to clean up call resources");
                        }

                        state = new State.Faulted(
                            connected.QueryHandlers, 
                            connected.Flow, 
                            connected.QueryExecutions, 
                            connected.SubscriptionQueryExecutions);
                        break;
                }
                break;
            case(_, Message.QueryHandlerCompleted handlerCompleted):
                switch (handlerCompleted.Result)
                {
                    case TaskResult.Ok:
                        _logger.LogDebug("Query handler completed for request '{RequestId}'", handlerCompleted.RequestId.ToString());
                        break;
                    case TaskResult.Error error:
                        _logger.LogDebug(error.Exception, "Query handler completed with an exception for request '{RequestId}'", handlerCompleted.RequestId.ToString());
                        break;
                }
                break;
            case (_, Message.QueryReplyForwardingCompleted forwardingCompleted):
            {
                switch (forwardingCompleted.Result)
                {
                    case TaskResult.Ok:
                        _logger.LogDebug("Query forwarding completed for request '{RequestId}'",
                            forwardingCompleted.RequestId.ToString());
                        break;
                    case TaskResult.Error error:
                        _logger.LogWarning(error.Exception,
                            "Query forwarding completed with an exception for request '{RequestId}'",
                            forwardingCompleted.RequestId.ToString());
                        break;
                }

                if (state.QueryExecutions.TryRemove(forwardingCompleted.RequestId, out var execution) && execution != null)
                {
                    execution.CancellationTokenSource.Dispose();
                }
                else
                {
                    _logger.LogWarning(
                        "Query execution could not be completed because no execution was found for request '{RequestId}'",
                        forwardingCompleted.RequestId.ToString());
                }
            }
                break;
            case (State.Disconnected, Message.RegisterQueryHandler register):
            {
                await _actor.TellAsync(new Message.Connect(), ct);
                state
                    .QueryHandlers
                    .RegisterQueryHandler(register.Id, register.Definition, register.Handler);

                var instruction =
                    state
                        .QueryHandlers
                        .TryBeginSubscribeToQueryInstruction(register.Definition);
                if (instruction != null)
                {
                    state
                        .QueryHandlers
                        .RegisterSubscribeToQueryCompletionSource(register.Definition, register.CompletionSource);

                     await _actor.TellAsync(new Message.SendQueryProviderOutbound(instruction), ct);
                }
                else
                {
                    register.CompletionSource.SetResult();
                }
            }
                break;
            case (State.Connected, Message.RegisterQueryHandler register):
            {
                state
                    .QueryHandlers
                    .RegisterQueryHandler(register.Id, register.Definition, register.Handler);

                var instruction =
                    state
                        .QueryHandlers
                        .TryBeginSubscribeToQueryInstruction(register.Definition);
                if (instruction != null)
                {
                    state
                        .QueryHandlers
                        .RegisterSubscribeToQueryCompletionSource(register.Definition, register.CompletionSource);

                    await _actor
                        .TellAsync(new Message.SendQueryProviderOutbound(instruction), ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    register.CompletionSource.SetResult();
                }

                if (state.QueryHandlers.RegisteredQueryCount == 1)
                {
                    await _actor
                        .ScheduleAsync(new Message.PurgeOverdueInstructions(), _purgeInterval, ct)
                        .ConfigureAwait(false);
                }
            }
                break;
            case (_, Message.RegisterQueryHandler register):
            {
                state
                    .QueryHandlers
                    .RegisterQueryHandler(register.Id, register.Definition, register.Handler);

                var instruction =
                    state
                        .QueryHandlers
                        .TryBeginSubscribeToQueryInstruction(register.Definition);
                if (instruction != null)
                {
                    state
                        .QueryHandlers
                        .RegisterSubscribeToQueryCompletionSource(register.Definition, register.CompletionSource);

                    await _actor
                        .TellAsync(new Message.SendQueryProviderOutbound(instruction), ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    register.CompletionSource.SetResult();
                }
            }
                break;
            case (State.Connected connected, Message.SendQueryProviderOutbound send):
                await connected.Call.RequestStream.WriteAsync(send.Instruction);
                break;
            case (State.Connecting connecting, Message.SendQueryProviderOutbound send):
                connecting.DeferredMessages.Add(send);
                _logger.LogWarning("Deferred send instruction '{InstructionId}' while connecting the query channel", send.Instruction.InstructionId);
                break;
            case (_, Message.SendQueryProviderOutbound send):
                _logger.LogWarning("Cannot send instruction '{InstructionId}' because the query channel is not connected", send.Instruction.InstructionId);
                break;
            case (_, Message.UnregisterQueryHandler unregister):
            {
                state.QueryHandlers.UnregisterQueryHandler(unregister.Id);
                var instruction =
                    state
                        .QueryHandlers
                        .TryBeginUnsubscribeFromQueryInstruction(unregister.Definition);
                if (instruction != null)
                {
                    state
                        .QueryHandlers
                        .RegisterUnsubscribeFromQueryCompletionSource(unregister.Definition, unregister.CompletionSource);

                    await _actor
                        .TellAsync(new Message.SendQueryProviderOutbound(instruction), ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    unregister.CompletionSource.SetResult();
                }
            }
                break;
            case (_, Message.PurgeOverdueInstructions):
                state.QueryHandlers.Purge(_purgeInterval);
                
                if (state.QueryHandlers.HasRegisteredQueries)
                {
                    await _actor
                        .ScheduleAsync(new Message.PurgeOverdueInstructions(), _purgeInterval, ct)
                        .ConfigureAwait(false);
                }
                break;
        }

        return state;
    }
#pragma warning restore CS4014
#pragma warning restore CA2016
// ReSharper enable MethodSupportsCancellation

    public PermitCount PermitsBatch { get; }
    public QueryService.QueryServiceClient Service { get; }
    
    private abstract record Message
    {
        public record Connect : Message;
        
        public record OpenStream : Message;

        public record StreamOpened(
            TaskResult<AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound>> Result) : Message;

        public record ReceiveQueryProviderInbound(
            TaskResult<QueryProviderInbound> Result) : Message;
        
        public record SendQueryProviderOutbound(QueryProviderOutbound Instruction) : Message;
        
        public record SendQueryProviderOutboundFaulted
            (QueryProviderOutbound Instruction, Exception Exception) : Message;
        
        public record QueryHandled(InstructionId RequestId, TaskResult<QueryResponse> Result) : Message;

        public record QueryCompleted(InstructionId RequestId, TaskResult Result) : Message;
        public record QueryHandlerCompleted(InstructionId RequestId, TaskResult Result) : Message;
        public record SubscriptionQueryHandlerCompleted(SubscriptionId SubscriptionIdentifier, TaskResult Result) : Message;

        public record QueryReplyForwardingCompleted(InstructionId RequestId, TaskResult Result) : Message;
        
        public record RegisterQueryHandler(
            RegisteredQueryId Id,
            QueryDefinition Definition,
            IQueryHandler Handler,
            TaskCompletionSource CompletionSource) : Message;

        public record UnregisterQueryHandler(
            RegisteredQueryId Id,
            QueryDefinition Definition,
            TaskCompletionSource CompletionSource) : Message;

        public record PurgeOverdueInstructions : Message;
        
        public record OnConnected : Message;
        public record Reconnect : Message;
    }

    private abstract record State(QueryHandlerCollection QueryHandlers, FlowController Flow, QueryExecutions QueryExecutions, SubscriptionQueryExecutions SubscriptionQueryExecutions)
    {
        public record Disconnected(QueryHandlerCollection QueryHandlers, FlowController Flow, QueryExecutions QueryExecutions, SubscriptionQueryExecutions SubscriptionQueryExecutions) : State(QueryHandlers, Flow, QueryExecutions, SubscriptionQueryExecutions);
        public record Connecting(List<Message> DeferredMessages, QueryHandlerCollection QueryHandlers, FlowController Flow, QueryExecutions QueryExecutions, SubscriptionQueryExecutions SubscriptionQueryExecutions) : State(QueryHandlers, Flow, QueryExecutions, SubscriptionQueryExecutions);
        public record Connected(
            AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound> Call,
            Task ConsumeQueryProviderInboundInstructions,
            CancellationTokenSource ConsumeQueryProviderInboundInstructionsCancellationTokenSource,
            OngoingQueryCollection OngoingQueries,
            QueryHandlerCollection QueryHandlers, FlowController Flow, QueryExecutions QueryExecutions, SubscriptionQueryExecutions SubscriptionQueryExecutions) : State(QueryHandlers, Flow, QueryExecutions, SubscriptionQueryExecutions);
        public record Faulted(QueryHandlerCollection QueryHandlers, FlowController Flow, QueryExecutions QueryExecutions, SubscriptionQueryExecutions SubscriptionQueryExecutions) : State(QueryHandlers, Flow, QueryExecutions, SubscriptionQueryExecutions);
    }
    
    public bool IsConnected => 
        !_actor.State.QueryHandlers.HasRegisteredQueries 
        || (_actor.State.QueryHandlers.HasRegisteredQueries && _actor.State is State.Connected);
    
    public async ValueTask Reconnect()
    {
        await _actor.TellAsync(new Message.Reconnect()).ConfigureAwait(false);
    }
    
    public async Task<IQueryHandlerRegistration> RegisterQueryHandlerAsync(IQueryHandler handler, params QueryDefinition[] queryDefinitions)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (queryDefinitions == null) throw new ArgumentNullException(nameof(queryDefinitions));
        if (queryDefinitions.Length == 0)
            throw new ArgumentException("The query definitions requires at least one query to be specified",
                nameof(queryDefinitions));
        
        var registeredQueries = queryDefinitions
            .Select(query => new { Id = RegisteredQueryId.New(), Query = query })
            .ToArray();

        var subscribeCompletions = new List<Task>();
        foreach (var registeredQuery in registeredQueries)
        {
            var subscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor
                .TellAsync(new Message.RegisterQueryHandler(
                    registeredQuery.Id,
                    registeredQuery.Query,
                    handler,
                    subscribeCompletionSource))
                .ConfigureAwait(false);
            subscribeCompletions.Add(subscribeCompletionSource.Task);
        }

        return new QueryHandlerRegistration(Task.WhenAll(subscribeCompletions), async () =>
        {
            var unsubscribeCompletions = new List<Task>();
            foreach (var registeredQuery in registeredQueries)
            {
                var unsubscribeCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await _actor
                    .TellAsync(new Message.UnregisterQueryHandler(
                        registeredQuery.Id,
                        registeredQuery.Query,
                        unsubscribeCompletionSource))
                    .ConfigureAwait(false);
                unsubscribeCompletions.Add(unsubscribeCompletionSource.Task);
            }

            await Task.WhenAll(unsubscribeCompletions).ConfigureAwait(false);
        });
    }

    public IAsyncEnumerable<QueryResponse> Query(QueryRequest query, CancellationToken ct)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        var request = new QueryRequest(query);
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = InstructionId.New().ToString();
        }
        try
        {
            var stream = Service.Query(query, cancellationToken: ct);
            return new QueryResponseConsumerStream(stream.ResponseStream.ReadAllAsync(ct), stream);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                _connection.ClientIdentity,
                ErrorCategory.QueryDispatchError,
                "An error occurred while attempting to dispatch a query",
                exception);
        }
    }

    public async Task<IQuerySubscriptionResult> SubscriptionQueryAsync(QueryRequest query, SerializedObject updateType, PermitCount bufferSize, PermitCount fetchSize, CancellationToken ct)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (updateType == null) throw new ArgumentNullException(nameof(updateType));
        var request = new QueryRequest(query);
        if (string.IsNullOrEmpty(request.MessageIdentifier))
        {
            request.MessageIdentifier = InstructionId.New().ToString();
        }
        try
        {
            var call = Service.Subscription(cancellationToken: ct);
            await call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                FlowControl = new SubscriptionQuery
                {
                    NumberOfPermits = bufferSize.ToInt64()
                }
            }).ConfigureAwait(false);
            await call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                Subscribe = new SubscriptionQuery
                {
                    QueryRequest = request,
                    SubscriptionIdentifier = request.MessageIdentifier,
                    UpdateResponseType = updateType
                }
            }).ConfigureAwait(false);

            return new QuerySubscriptionResult(
                _connection.ClientIdentity, 
                request,
                PermitsBatch, 
                call,
                _loggerFactory,
                _actor.CancellationToken);
        }
        catch (Exception exception)
        {
            throw new AxonServerException(
                _connection.ClientIdentity,
                ErrorCategory.QueryDispatchError,
                "An error occurred while attempting to dispatch a query",
                exception);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await _actor.DisposeAsync();
    }
}