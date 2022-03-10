using AutoFixture;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

// public class CommandSubscriptionManagementTests
// {
//     private readonly Fixture _fixture;
//     private readonly ClientIdentity _clientIdentity;
//
//     public CommandSubscriptionManagementTests()
//     {
//         _fixture = new Fixture();
//         _fixture.CustomizeClientInstanceId();
//         _fixture.CustomizeComponentName();
//         _fixture.CustomizeLoadFactor();
//
//         _clientIdentity = new ClientIdentity(
//             _fixture.Create<ComponentName>(), 
//             _fixture.Create<ClientInstanceId>(), 
//             _fixture.Create<Dictionary<string, string>>(), 
//             _fixture.Create<Version>());
//     }
//     
//     [Fact]
//     public async Task SubscribeCommandHandlerHasExpectedResult()
//     {
//         var writer = new SubscribeWriter();
//         var loadFactor = _fixture.Create<LoadFactor>();
//         var commands = _fixture
//             .CreateMany<CommandName>(Random.Shared.Next(1, 5))
//             .ToArray();
//
//         var sut = new CommandSubscriptionManagement(_clientIdentity, writer.WriteAsync);
//
//         await sut.SubscribeCommandHandler(
//             CommandHandlerId.New(),
//             (command, ct) => Task.FromResult(new CommandResponse()),
//             loadFactor,
//             commands);
//
//         Assert.Equal(
//             commands
//                 .Select(command =>
//                     new CommandSubscription
//                     {
//                         ClientId = _clientIdentity.ClientInstanceId.ToString(),
//                         ComponentName = _clientIdentity.ComponentName.ToString(),
//                         LoadFactor = loadFactor.ToInt32(),
//                         Command = command.ToString()
//                     }
//                 )
//                 .ToList()
//             , writer.Written
//             , new CommandSubscriptionComparer());
//     }
//     
//     [Fact]
//     public async Task AcknowledgeKnownSubscribeInstructionHasExpectedResult()
//     {
//         var writer = new SubscribeWriter();
//         var loadFactor = _fixture.Create<LoadFactor>();
//         var commands = _fixture
//             .CreateMany<CommandName>(Random.Shared.Next(1, 5))
//             .ToArray();
//
//         var sut = new CommandSubscriptionManagement(_clientIdentity, writer.WriteAsync);
//
//         await sut.SubscribeCommandHandler(
//             CommandHandlerId.New(),
//             (command, ct) => Task.FromResult(new CommandResponse()),
//             loadFactor,
//             commands);
//
//         var instruction = new InstructionId(writer.Written[Random.Shared.Next(0, writer.Written.Count)].MessageId);
//
//         await sut.Acknowledge(InstructionAck acknowledgement);
//
//         Assert.Equal(
//             commands
//                 .Select(command =>
//                     new CommandSubscription
//                     {
//                         ClientId = _clientIdentity.ClientInstanceId.ToString(),
//                         ComponentName = _clientIdentity.ComponentName.ToString(),
//                         LoadFactor = loadFactor.ToInt32(),
//                         Command = command.ToString()
//                     }
//                 )
//                 .ToList()
//             , writer.Written
//             , new CommandSubscriptionComparer());
//     }
//     
//     [Fact]
//     public async Task AcknowledgeUnknownSubscribeInstructionHasExpectedResult()
//     {
//         var writer = new SubscribeWriter();
//         var loadFactor = _fixture.Create<LoadFactor>();
//         var commands = _fixture
//             .CreateMany<CommandName>(Random.Shared.Next(1, 5))
//             .ToArray();
//
//         var sut = new CommandSubscriptionManagement(_clientIdentity, writer.WriteAsync);
//
//         await sut.SubscribeCommandHandler(
//             CommandHandlerId.New(),
//             (command, ct) => Task.FromResult(new CommandResponse()),
//             loadFactor,
//             commands);
//
//         Assert.Equal(
//             commands
//                 .Select(command =>
//                     new CommandSubscription
//                     {
//                         ClientId = _clientIdentity.ClientInstanceId.ToString(),
//                         ComponentName = _clientIdentity.ComponentName.ToString(),
//                         LoadFactor = loadFactor.ToInt32(),
//                         Command = command.ToString()
//                     }
//                 )
//                 .ToList()
//             , writer.Written
//             , new CommandSubscriptionComparer());
//     }
//
//     [Fact]
//     public async Task AcknowledgeAllSubscribeInstructionsOfCommandHandlerHasExpectedResult()
//     {
//         
//     }
//
//     private class Writer
//     {
//         public Writer()
//         {
//             InstructionsWritten = new List<CommandProviderOutbound>();
//         }
//         
//         public List<CommandProviderOutbound> InstructionsWritten { get; }
//         
//         public ValueTask WriteAsync(CommandProviderOutbound instruction)
//         {
//             InstructionsWritten.Add(instruction);
//             return ValueTask.CompletedTask;
//         }
//     }
//     
//     private class SubscribeWriter
//     {
//         private readonly List<CommandSubscription> _written;
//
//         public SubscribeWriter()
//         {
//             _written = new List<CommandSubscription>();
//         }
//
//         public IReadOnlyList<CommandSubscription> Written => _written;
//
//         public ValueTask WriteAsync(CommandProviderOutbound instruction)
//         {
//             if (instruction.Subscribe != null)
//             {
//                 _written.Add(instruction.Subscribe);
//             }
//             return ValueTask.CompletedTask;
//         }
//     }
//     
//     public class CommandSubscriptionComparer : IEqualityComparer<CommandSubscription>
//     {
//         public bool Equals(CommandSubscription? x, CommandSubscription? y)
//         {
//             if (x is null && y is null) return true;
//             if (x is null || y is null) return false;
//             return x.Command.Equals(y.Command)
//                    && x.ClientId.Equals(y.ClientId)
//                    && x.ComponentName.Equals(y.ComponentName)
//                    && x.LoadFactor.Equals(y.LoadFactor);
//         }
//
//         public int GetHashCode(CommandSubscription obj)
//         {
//             throw new NotSupportedException();
//         }
//     }
// }
//
//
//
// public class CommandSubscriptionManagement
// {
//     private readonly ClientIdentity _clientIdentity;
//     private readonly WriteCommandProviderOutbound _writer;
//
//     private readonly Dictionary<CommandHandlerId, TaskCompletionSource> _commandHandlerSubscribeCompletionMap;
//     private readonly Dictionary<CommandHandlerId, HashSet<CommandName>> _subscribeCommandHandlerCountdownMap;
//
//     private readonly Dictionary<InstructionId, (CommandHandlerId, CommandName)>
//         _subscribeInstructionToCommandHandlerMap;
//
//     // public Dictionary<SubscriptionId, Subscription> All = new Dictionary<SubscriptionId, Subscription>();
//     // public Dictionary<CommandName, SubscriptionId> Active = new Dictionary<CommandName, SubscriptionId>();
//     // public Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>> Handlers = new Dictionary<CommandName, Func<Command, CancellationToken, Task<CommandResponse>>>();
//     // public Dictionary<InstructionId, SubscriptionId> Subscribing = new Dictionary<InstructionId, SubscriptionId>();
//     // public Dictionary<InstructionId, SubscriptionId> Unsubscribing = new Dictionary<InstructionId, SubscriptionId>();
//     // public HashSet<SubscriptionId> Superseded = new HashSet<SubscriptionId>();
//
//     public CommandSubscriptionManagement(ClientIdentity clientIdentity, WriteCommandProviderOutbound writer)
//     {
//         _clientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
//         _writer = writer ?? throw new ArgumentNullException(nameof(writer));
//
//         _commandHandlerSubscribeCompletionMap = new Dictionary<CommandHandlerId, TaskCompletionSource>();
//         _subscribeCommandHandlerCountdownMap = new Dictionary<CommandHandlerId, HashSet<CommandName>>();
//         _subscribeInstructionToCommandHandlerMap = new Dictionary<InstructionId, (CommandHandlerId, CommandName)>();
//     }
//
//     public async Task SubscribeCommandHandler(CommandHandlerId commandHandlerId,
//         Func<Command, CancellationToken, Task<CommandResponse>> handler, LoadFactor loadFactor,
//         CommandName[] commandNames, TaskCompletionSource subscribeCompletionSource)
//     {
//         _commandHandlerSubscribeCompletionMap.Add(commandHandlerId, subscribeCompletionSource);
//         _subscribeCommandHandlerCountdownMap.Add(commandHandlerId, new HashSet<CommandName>(commandNames));
//         foreach (var command in commandNames)
//         {
//             var instructionId = InstructionId.New();
//             _subscribeInstructionToCommandHandlerMap.Add(instructionId, (commandHandlerId, command));
//             await _writer(new CommandProviderOutbound
//             {
//                 InstructionId = instructionId.ToString(),
//                 Subscribe = new CommandSubscription
//                 {
//                     Command = command.ToString(),
//                     ClientId = _clientIdentity.ClientInstanceId.ToString(),
//                     ComponentName = _clientIdentity.ComponentName.ToString(),
//                     LoadFactor = loadFactor.ToInt32(),
//                     MessageId = instructionId.ToString()
//                 }
//             });
//
//         }
//     }
//
//     public void Acknowledge(InstructionAck acknowledgement)
//     {
//         if (_subscribeInstructionToCommandHandlerMap.TryGetValue(new InstructionId(acknowledgement.InstructionId),
//                 out var item) && item is var (commandHandlerId, commandName))
//         {
//             if (_subscribeCommandHandlerCountdownMap.TryGetValue(commandHandlerId, out var countdown))
//             {
//                 countdown.Remove(commandName);
//                 if (countdown.Count == 0)
//                 {
//                     _subscribeCommandHandlerCountdownMap.Remove(commandHandlerId);
//                     if (_commandHandlerSubscribeCompletionMap.TryGetValue(commandHandlerId,
//                             out var subscribeCompletionSource))
//                     {
//                         subscribeCompletionSource.SetResult();
//                     }
//                 }
//             }
//         }
//     }
//
//     // public class CommandHandler
//     // {
//     //     public CommandHandlerId Id { get; }
//     //     public Func<Command, CancellationToken, Task<CommandResponse>> Handler { get; }
//     //     public LoadFactor LoadFactor { get; }
//     //     public CommandName[] CommandNames { get; }
//     //     public TaskCompletionSource SubscribeCompletionSource { get; }
//     //     
//     //     public CommandHandler(
//     //         CommandHandlerId id,
//     //         Func<Command, CancellationToken, Task<CommandResponse>> handler,
//     //         LoadFactor loadFactor,
//     //         CommandName[] commandNames,
//     //         TaskCompletionSource subscribeCompletionSource)
//     //     {
//     //         Id = id;
//     //         Handler = handler;
//     //         LoadFactor = loadFactor;
//     //         CommandNames = commandNames;
//     //         SubscribeCompletionSource = subscribeCompletionSource;
//     //     }
//     //
//     //     public void Acknowledge(CommandName commandName, InstructionAck acknowledgement)
//     //     {
//     //            
//     //     }
//     //
//     //     public class CommandSubscriptions
//     //     {
//     //         public CommandSubscriptions()
//     //         {
//     //             
//     //         }
//     //     }
//     // }
//     
//     
// }
//     
