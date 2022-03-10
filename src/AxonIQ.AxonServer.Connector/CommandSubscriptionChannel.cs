// using System.Collections.Concurrent;
// using System.Diagnostics.Contracts;
// using System.Threading.Channels;
// using AxonIQ.AxonServer.Grpc;
// using AxonIQ.AxonServer.Grpc.Command;
// using Microsoft.Extensions.Logging;
//
// namespace AxonIQ.AxonServer.Connector;
//
// public class CommandSubscriptionChannel : IAsyncDisposable
// {
//     private readonly WriteCommandProviderOutbound _writer;
//     private readonly ILogger<CommandSubscriptionChannel> _logger;
//     
//     private readonly Channel<Protocol> _inbox;
//     private readonly CancellationTokenSource _inboxCancellation;
//     private readonly Task _protocol;
//     private readonly ConcurrentDictionary<Task, Task> _runningCommands;
//
//     public CommandSubscriptionChannel(
//         WriteCommandProviderOutbound writer,
//         ILogger<CommandSubscriptionChannel> logger)
//     {
//         _writer = writer ?? throw new ArgumentNullException(nameof(writer));
//         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//         
//         _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
//         {
//             SingleReader = true,
//             SingleWriter = false
//         });
//         _inboxCancellation = new CancellationTokenSource();
//         _runningCommands = new ConcurrentDictionary<Task, Task>();
//         _protocol = RunChannelProtocol(_inboxCancellation.Token);
//     }
//     
//     private async Task RunChannelProtocol(CancellationToken ct)
//     {
//         var all = new Dictionary<SubscriptionId, Subscription>();
//         var active = new Dictionary<CommandName, SubscriptionId>();
//         var handlers = new Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>>();
//         var subscribing = new Dictionary<InstructionId, SubscriptionId>();
//         var unsubscribing = new Dictionary<InstructionId, SubscriptionId>();
//         var superseded = new HashSet<SubscriptionId>();
//         try
//         {
//             while (await _inbox.Reader.WaitToReadAsync(ct))
//             {
//                 while (_inbox.Reader.TryRead(out var message))
//                 {
//                     _logger.LogDebug("Began {Message}", message.ToString());
//                     switch (message)
//                     {
//                         case Protocol.Subscribe subscribe:
//                         {
//                             all.Add(
//                                 subscribe.SubscriptionId,
//                                 new Subscription(
//                                     subscribe.SubscriptionId,
//                                     subscribe.Name,
//                                     subscribe.LoadFactor,
//                                     subscribe.Handler));
//                             var subscribeInstructionId = InstructionId.New();
//                             subscribing.Add(subscribeInstructionId, subscribe.SubscriptionId);
//                             if (active.TryGetValue(subscribe.Name, out var activeSubscriptionId))
//                             {
//                                 // If we're in the process of subscribing or unsubscribing
//                                 // allow that to complete gracefully, by putting the active
//                                 // subscription on the superseded list. Otherwise, just remove it from our memory.
//                                 if (subscribing.ContainsValue(activeSubscriptionId) ||
//                                     unsubscribing.ContainsValue(activeSubscriptionId))
//                                 {
//                                     superseded.Add(activeSubscriptionId);
//                                 }
//                                 else
//                                 {
//                                     all.Remove(activeSubscriptionId);
//                                 }
//                             }
//                             active[subscribe.Name] = subscribe.SubscriptionId;
//                             handlers[subscribe.Name] = subscribe.Handler;
//                             break;
//                         }
//                         case Protocol.ReceiveCommand receiveCommand:
//                         {
//                             var name = new CommandName(receiveCommand.Command.Name);
//                             if (handlers.TryGetValue(name, out var handler))
//                             {
//                                 var runningCommand = handler(receiveCommand.Command, ct);
//                                 if (_runningCommands.TryAdd(runningCommand, runningCommand))
//                                 {
// #pragma warning disable CS4014
//                                     runningCommand
//                                         .ContinueWith(continuation =>
//                                         {
//                                             if (!_runningCommands.TryRemove(continuation, out _))
//                                             {
//                                                 _logger.LogWarning(
//                                                     "Removing the running command task from the list of running commands failed");
//                                             }
//
//                                             if (!continuation.IsCanceled)
//                                             {
//                                                 if (continuation.IsFaulted)
//                                                 {
//                                                     var response = new CommandResponse
//                                                     {
//                                                         ErrorCode = ErrorCategory.CommandExecutionError.ToString(),
//                                                         ErrorMessage = new ErrorMessage
//                                                         {
//                                                             Details =
//                                                             {
//                                                                 continuation.Exception?.ToString() ?? ""
//                                                             },
//                                                             Location = "Client",
//                                                             Message = continuation.Exception?.Message ?? ""
//                                                         },
//                                                         RequestIdentifier = receiveCommand.Command.MessageIdentifier
//                                                     };
//                                                     if (!_inbox.Writer.TryWrite(
//                                                             new Protocol.CompletedCommand(response)))
//                                                     {
//                                                         _logger.LogWarning("Could not tell the command subscription channel that handling a command was completed because its inbox refused to accept the message");
//                                                     }
//                                                 }
//                                                 else if (continuation.IsCompletedSuccessfully)
//                                                 {
//                                                     var response = new CommandResponse(continuation.Result)
//                                                     {
//                                                         RequestIdentifier = receiveCommand.Command.MessageIdentifier
//                                                     };
//                                                     if (!_inbox.Writer.TryWrite(
//                                                             new Protocol.CompletedCommand(response)))
//                                                     {
//                                                         _logger.LogWarning("Could not tell the command subscription channel that handling a command was completed because its inbox refused to accept the message");
//                                                     }
//                                                 }
//                                                 else
//                                                 {
//                                                     _logger.LogWarning("Handling a command completed in an unexpected way and a response will not be sent");
//                                                 }
//                                             }
//                                             else
//                                             {
//                                                 _logger.LogDebug("Handling a command was cancelled and a response will not be sent");
//                                             }
//                                         }, ct)
//                                         .ConfigureAwait(false);
// #pragma warning restore CS4014
//                                 }
//                                 else
//                                 {
//                                     _logger.LogWarning("Adding the running command task to the list of running commands failed");
//                                 }
//                             }
//                             else
//                             {
//                                 // _writer(new CommandProviderOutbound{ Ack = new InstructionAck { InstructionId = receiveCommand.}})
//                                 // _inbox.Writer.WriteAsync()
//                             }
//
//                             break;
//                         }
//                     }
//                     _logger.LogDebug("Completed {Message}", message.ToString());
//                 }
//             }
//         }
//         catch (TaskCanceledException exception)
//         {
//             _logger.LogDebug(exception,
//                 "Command channel message loop is exciting because a task was cancelled");
//         }
//         catch (OperationCanceledException exception)
//         {
//             _logger.LogDebug(exception,
//                 "Command channel message loop is exciting because an operation was cancelled");
//         }
//     }
//     
//     private record Subscription(
//         SubscriptionId SubscriptionId,
//         CommandName Command,
//         LoadFactor LoadFactor,
//         Func<Command, CancellationToken, Task<CommandResponse>> Handler);
//     
//     private record Protocol
//     {
//         public record Subscribe(SubscriptionId SubscriptionId, CommandName Name, LoadFactor LoadFactor,
//             Func<Command, CancellationToken, Task<CommandResponse>> Handler) : Protocol;
//         public record Unsubscribe(SubscriptionId SubscriptionId) : Protocol;
//         public record ReceiveAcknowledgement(InstructionAck Message) : Protocol;
//         public record ReceiveCommand(InstructionId InstructionId, Command Command) : Protocol;
//
//         public record CompletedCommand(CommandResponse Response) : Protocol;
//     }
//
//     public ValueTask SubscribeToCommand(
//         SubscriptionId subscriptionId, 
//         CommandName name, 
//         LoadFactor loadFactor,
//         Func<Command, CancellationToken, Task<CommandResponse>> handler)
//     {
//         return _inbox.Writer.WriteAsync(new Protocol.Subscribe(subscriptionId, name, loadFactor, handler));
//     }
//     
//     public ValueTask UnsubscribeFromCommand(string SubscriptionId, CommandName Command)
//     {
//         return ValueTask.CompletedTask;
//     }
//     
//     public async ValueTask DisposeAsync()
//     {
//         _inboxCancellation.Cancel();
//         _inbox.Writer.Complete();
//         await _inbox.Reader.Completion;
//         await _protocol;
//         _inboxCancellation.Dispose();
//         _protocol.Dispose();
//     }
// }