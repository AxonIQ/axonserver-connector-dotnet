/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Query;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class QueryChannel : IQueryChannel, IAsyncDisposable
{
    private readonly Context _context;
    private readonly PermitCount _permits;
    private readonly PermitCount _permitsBatch;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CallInvoker _callInvoker;
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
        Clock = clock;
        _context = context;
        _callInvoker = callInvoker;
        _loggerFactory = loggerFactory;
        Service = new QueryService.QueryServiceClient(callInvoker);
        _logger = loggerFactory.CreateLogger<QueryChannel>();
        _permits = permits;
        _permitsBatch = permitsBatch;

        _state = new State.Disconnected();
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
            case State.Disconnected:
                try
                {
                    var stream = Service.OpenStream(cancellationToken: ct);
                    if (stream != null)
                    {
                        _logger.LogInformation(
                            "Opened query stream for context '{Context}'",
                            _context);

                        await stream.RequestStream.WriteAsync(new QueryProviderOutbound
                        {
                            FlowControl = new FlowControl
                            {
                                ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                Permits = _permits.ToInt64()
                            }
                        });

                        _state = new State.Connected(
                            stream,
                            ConsumeResponseStream(stream.ResponseStream, ct)
                        );
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Could not open query stream for context '{Context}'",
                            _context);
                    }
                }
                catch (RpcException exception) when (exception.StatusCode == StatusCode.Unavailable)
                {
                    _logger.LogWarning(
                        "Could not open query stream for context '{Context}': no connection to AxonServer",
                        _context.ToString());
                }

                break;
            case State.Connected:
                _logger.LogDebug("QueryChannel for context '{Context}' is already connected",
                    _context.ToString());
                break;
        }
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        var subscriptions = new QuerySubscriptions(ClientIdentity, Clock);
        var flowController = new FlowController(_permitsBatch);
        var queriesInFlight = new Dictionary<QueryRequest, Task>();
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
                                    subscriptions.RegisterQueryHandler(
                                        subscribe.QueryHandlerId,
                                        subscribe.CompletionSource,
                                        subscribe.Handler);
                                    
                                    foreach (var (subscriptionId, query) in subscribe.SubscribedQueries)
                                    {
                                        _logger.LogInformation(
                                            "Registered handler for query '{Query}' in context '{Context}'",
                                            query.QueryName.ToString(), _context.ToString());
                                    
                                        var instructionId = subscriptions.SubscribeToQuery(
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
                                        await connected.Stream.RequestStream.WriteAsync(request);
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
                                    // if (!unsubscribe.CompletionSource.Fault(
                                    //         new AxonServerException(
                                    //             ClientIdentity,
                                    //             ErrorCategory.Other,
                                    //             "Unable to unsubscribe commands and handler: no connection to AxonServer")))
                                    // {
                                    //     _logger.LogWarning(
                                    //         "Could not fault the unsubscribe completion source of command handler '{CommandHandlerId}'",
                                    //         unsubscribe.CommandHandlerId.ToString());
                                    // }

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
                                            subscriptions.Acknowledge(receive.Message.Ack);
                                            
                                            if (flowController.Increment())
                                            {
                                                await connected.Stream.RequestStream.WriteAsync(new QueryProviderOutbound
                                                {
                                                    FlowControl = new FlowControl
                                                    {
                                                        ClientId = ClientIdentity.ClientInstanceId.ToString(),
                                                        Permits = _permitsBatch.ToInt64()
                                                    }
                                                });
                                            }
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.Query:
                                            if (subscriptions.ActiveHandlers.TryGetValue(
                                                    new QueryName(receive.Message.Query.Query), out var handlers))
                                            {
                                                foreach (var handler in handlers)
                                                {
                                                    queriesInFlight.Add(receive.Message.Query,
                                                        handler.Handle(receive.Message.Query, new QueryResponseChannel(
                                                            receive.Message.Query,
                                                            instruction =>
                                                                _channel.Writer.WriteAsync(
                                                                    new Protocol.SendQueryProviderOutbound(instruction),
                                                                    ct))));
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
                                                });
                                            }
                                            break;
                                        case QueryProviderInbound.RequestOneofCase.SubscriptionQueryRequest:
                                            switch(receive.Message.SubscriptionQueryRequest.RequestCase)
                                            {
                                                case SubscriptionQueryRequest.RequestOneofCase.None:
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.Subscribe:
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.Unsubscribe:
                                                    break;
                                                case SubscriptionQueryRequest.RequestOneofCase.GetInitialResult:
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
                                    await connected.Stream.RequestStream.WriteAsync(send.Instruction);
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
    public Func<DateTimeOffset> Clock { get; }
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
        public record Disconnected : State;

        public record Connected(
            AsyncDuplexStreamingCall<QueryProviderOutbound, QueryProviderInbound> Stream,
            Task ConsumeResponseStreamLoop) : State;
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

    public async Task<IQuerySubscriptionResult> SubscribeToQuery(QueryRequest query, SerializedObject updateType, PermitCount bufferSize, PermitCount fetchSize, CancellationToken ct)
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
            });
            await call.RequestStream.WriteAsync(new SubscriptionQueryRequest
            {
                FlowControl = new SubscriptionQuery
                {
                    NumberOfPermits = bufferSize.ToInt64()
                }
            });

            return new QuerySubscriptionResult(
                ClientIdentity, 
                _permitsBatch, 
                call,
                _loggerFactory.CreateLogger<QuerySubscriptionResult>(),
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