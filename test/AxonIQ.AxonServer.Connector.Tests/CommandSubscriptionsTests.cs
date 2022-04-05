using AutoFixture;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Command;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CommandSubscriptionsTests
{
    private readonly Fixture _fixture;
    private readonly ClientIdentity _clientIdentity;
    private readonly Func<DateTimeOffset> _clock;

    public CommandSubscriptionsTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeLoadFactor();
        _fixture.CustomizeCommandHandlerId();
        _fixture.CustomizeSubscriptionId();
        _fixture.CustomizeLoadFactor();

        _clientIdentity = new ClientIdentity(
            _fixture.Create<ComponentName>(),
            _fixture.Create<ClientInstanceId>(),
            _fixture.Create<Dictionary<string, string>>(),
            _fixture.Create<Version>());
        _clock = () => DateTimeOffset.UtcNow;
    }

    [Fact]
    public void RegisterCommandHandlerHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<CommandHandlerId>();
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource);

        Assert.Equal(
            new[] { new KeyValuePair<CommandHandlerId, CountdownCompletionSource>(handlerId, completionSource) },
            sut.CompletionSources);
    }
    
    [Fact]
    public void RegisterCommandHandlerMultipleTimesHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<CommandHandlerId>();
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource);
        sut.RegisterCommandHandler(handlerId, completionSource);

        Assert.Equal(
            new[] { new KeyValuePair<CommandHandlerId, CountdownCompletionSource>(handlerId, completionSource) },
            sut.CompletionSources);
    }

    [Fact]
    public void SubscribeToCommandHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<SubscriptionId>();
        var handlerId = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );

        Assert.True(instructionId.HasValue);
        Assert.Equal(
            new[]
            {
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId,
                    new CommandSubscriptions.Subscription(
                        handlerId,
                        subscriptionId,
                        command,
                        loadFactor,
                        handler
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { new KeyValuePair<InstructionId, SubscriptionId>(instructionId!.Value, subscriptionId) },
            sut.SubscribeInstructions);
    }

    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribedWithSameCommandHandlerHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<SubscriptionId>();
        var handlerId = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId1 = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );
        
        var instructionId2 = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId1.HasValue);
        Assert.False(instructionId2.HasValue);
        Assert.Equal(
            new[]
            {
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId,
                    new CommandSubscriptions.Subscription(
                        handlerId,
                        subscriptionId,
                        command,
                        loadFactor,
                        handler
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, SubscriptionId>(instructionId1!.Value, subscriptionId) },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<SubscriptionId>();
        var handlerId1 = _fixture.Create<CommandHandlerId>();
        var subscriptionId2 = _fixture.Create<SubscriptionId>();
        var handlerId2 = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId1 = sut.SubscribeToCommand(
            subscriptionId1,
            handlerId1,
            command,
            loadFactor,
            handler
        );
        
        var instructionId2 = sut.SubscribeToCommand(
            subscriptionId2,
            handlerId2,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId1.HasValue);
        Assert.True(instructionId2.HasValue);
        Assert.Equal(
            new[]
            {
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId1,
                    new CommandSubscriptions.Subscription(
                        handlerId1,
                        subscriptionId1,
                        command,
                        loadFactor,
                        handler
                    )),
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId2,
                    new CommandSubscriptions.Subscription(
                        handlerId2,
                        subscriptionId2,
                        command,
                        loadFactor,
                        handler
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, SubscriptionId>(instructionId1!.Value, subscriptionId1),
                new KeyValuePair<InstructionId, SubscriptionId>(instructionId2!.Value, subscriptionId2) 
            },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribeAndAcknowledgedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<SubscriptionId>();
        var handlerId1 = _fixture.Create<CommandHandlerId>();
        var subscriptionId2 = _fixture.Create<SubscriptionId>();
        var handlerId2 = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId1 = sut.SubscribeToCommand(
            subscriptionId1,
            handlerId1,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId1.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId1!.Value.ToString()
        });
        
        var instructionId2 = sut.SubscribeToCommand(
            subscriptionId2,
            handlerId2,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId2.HasValue);
        Assert.Equal(
            new[]
            {
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId1,
                    new CommandSubscriptions.Subscription(
                        handlerId1,
                        subscriptionId1,
                        command,
                        loadFactor,
                        handler
                    )),
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId2,
                    new CommandSubscriptions.Subscription(
                        handlerId2,
                        subscriptionId2,
                        command,
                        loadFactor,
                        handler
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, SubscriptionId>(instructionId2!.Value, subscriptionId2) 
            },
            sut.SubscribeInstructions);
        Assert.Equal(
            new []
            {
                new KeyValuePair<CommandName,SubscriptionId>(command, subscriptionId1)
            },
            sut.ActiveSubscriptions);
    }

    [Fact]
    public void AcknowledgeWhenAlreadySubscribeAndAcknowledgedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<SubscriptionId>();
        var handlerId1 = _fixture.Create<CommandHandlerId>();
        var subscriptionId2 = _fixture.Create<SubscriptionId>();
        var handlerId2 = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId1 = sut.SubscribeToCommand(
            subscriptionId1,
            handlerId1,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId1.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId1!.Value.ToString()
        });
        
        var instructionId2 = sut.SubscribeToCommand(
            subscriptionId2,
            handlerId2,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId2.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId2!.Value.ToString()
        });
        
        Assert.Equal(
            new[]
            {
                new KeyValuePair<SubscriptionId, CommandSubscriptions.Subscription>(subscriptionId2,
                    new CommandSubscriptions.Subscription(
                        handlerId2,
                        subscriptionId2,
                        command,
                        loadFactor,
                        handler
                    ))
            }, sut.AllSubscriptions);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.Equal(
            new []
            {
                new KeyValuePair<CommandName,SubscriptionId>(command, subscriptionId2)
            },
            sut.ActiveSubscriptions);
    }
    
    [Fact]
    public void AcknowledgeSubscribeToCommandWithUnknownInstructionIdHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<SubscriptionId>();
        var handlerId = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var instructionId = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = _fixture.Create<bool>(), 
            InstructionId = InstructionId.New().ToString(),
            Error = new ErrorMessage
            {
                ErrorCode = ErrorCategory.All[Random.Shared.Next(0, ErrorCategory.All.Count)].ToString(),
                Message = _fixture.Create<string>(),
                Location = _fixture.Create<string>()
            }
        });
        
        Assert.Empty(sut.ActiveSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, SubscriptionId>(instructionId!.Value, subscriptionId) 
            },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void AcknowledgeSuccessfulSubscribeToCommandWithKnownInstructionIdHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<SubscriptionId>();
        var handlerId = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource);
        
        var instructionId = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId!.Value.ToString(),
            Error = new ErrorMessage
            {
                ErrorCode = ErrorCategory.All[Random.Shared.Next(0, ErrorCategory.All.Count)].ToString(),
                Message = _fixture.Create<string>(),
                Location = _fixture.Create<string>()
            }
        });
        
        Assert.Equal(
            new [] { new KeyValuePair<CommandName,SubscriptionId>(command, subscriptionId)},
            sut.ActiveSubscriptions);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.True(completionSource.Completion.IsCompletedSuccessfully);
    }
    
    [Fact]
    public void AcknowledgeUnsuccessfulSubscribeToCommandWithKnownInstructionIdHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<SubscriptionId>();
        var handlerId = _fixture.Create<CommandHandlerId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource);
        
        var instructionId = sut.SubscribeToCommand(
            subscriptionId,
            handlerId,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId.HasValue);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = false, 
            InstructionId = instructionId!.Value.ToString(),
            Error = new ErrorMessage
            {
                ErrorCode = ErrorCategory.All[Random.Shared.Next(0, ErrorCategory.All.Count)].ToString(),
                Message = _fixture.Create<string>(),
                Location = _fixture.Create<string>()
            }
        });
        
        Assert.Empty(sut.ActiveSubscriptions);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.True(completionSource.Completion.IsFaulted);
    }
    
    [Fact]
    public void SubscribeToCommandMultipleTimesWithOutOfOrderAcknowledgementsHasExpectedResult()
    {
        var sut = new CommandSubscriptions(_clientIdentity, _clock);
        
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (command, ct) => Task.FromResult(new CommandResponse());
        
        var subscriptionId1 = _fixture.Create<SubscriptionId>();
        var handlerId1 = _fixture.Create<CommandHandlerId>();
        var completionSource1 = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId1, completionSource1);
        
        var subscriptionId2 = _fixture.Create<SubscriptionId>();
        var handlerId2 = _fixture.Create<CommandHandlerId>();
        var completionSource2 = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId2, completionSource2);
        
        var instructionId1 = sut.SubscribeToCommand(
            subscriptionId1,
            handlerId1,
            command,
            loadFactor,
            handler
        );
        
        Assert.Empty(sut.SupersededSubscriptions);
        
        var instructionId2 = sut.SubscribeToCommand(
            subscriptionId2,
            handlerId2,
            command,
            loadFactor,
            handler
        );
        
        Assert.True(instructionId1.HasValue);
        Assert.True(instructionId2.HasValue);
        Assert.Equal(
            new [] { subscriptionId1 },
            sut.SupersededSubscriptions);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId2!.Value.ToString(),
        });
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId1!.Value.ToString(),
        });
        
        Assert.Equal(
            new [] { new KeyValuePair<CommandName, SubscriptionId>(command, subscriptionId2)},
            sut.ActiveSubscriptions);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.Empty(sut.SupersededSubscriptions);
        Assert.True(completionSource1.Completion.IsCompletedSuccessfully);
        Assert.True(completionSource2.Completion.IsCompletedSuccessfully);
    }
}
