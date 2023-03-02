using AutoFixture;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
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
        _fixture.CustomizeRegistrationId();
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
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<RegistrationId>();
        var completionSource = new CountdownCompletionSource(1);
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (_, _) => Task.FromResult(new CommandResponse());
        
        sut.RegisterCommandHandler(handlerId, completionSource, loadFactor, handler);

        Assert.Equal(
            new[] { new KeyValuePair<RegistrationId, CountdownCompletionSource>(handlerId, completionSource) },
            sut.SubscribeCompletionSources);
        Assert.Equal(
            new[] { new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommandHandler>(handlerId, new CommandRegistrations.RegisteredCommandHandler(handlerId, loadFactor, handler))},
            sut.AllCommandHandlers);
    }
    
    [Fact]
    public void RegisterCommandHandlerMultipleTimesHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<RegistrationId>();
        var completionSource = new CountdownCompletionSource(1);
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (_, _) => Task.FromResult(new CommandResponse());
        
        sut.RegisterCommandHandler(handlerId, completionSource, loadFactor, handler);
        sut.RegisterCommandHandler(handlerId, completionSource, loadFactor, handler);

        Assert.Equal(
            new[] { new KeyValuePair<RegistrationId, CountdownCompletionSource>(handlerId, completionSource) },
            sut.SubscribeCompletionSources);
        Assert.Equal(
            new[] { new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommandHandler>(handlerId, new CommandRegistrations.RegisteredCommandHandler(handlerId, loadFactor, handler))},
            sut.AllCommandHandlers);
    }

    [Fact]
    public void SubscribeToCommandHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        var instructionId = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);

        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId,
                        subscriptionId,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { new KeyValuePair<InstructionId, RegistrationId>(instructionId, subscriptionId) },
            sut.SubscribeInstructions);
    }

    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribedWithSameCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        var instructionId1 = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);
        
        var instructionId2 = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);
        
        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId,
                        subscriptionId,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, RegistrationId>(instructionId1, subscriptionId),
                new KeyValuePair<InstructionId, RegistrationId>(instructionId2, subscriptionId)
            },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<RegistrationId>();
        var handlerId1 = _fixture.Create<RegistrationId>();
        var subscriptionId2 = _fixture.Create<RegistrationId>();
        var handlerId2 = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        var instructionId1 = sut.SubscribeToCommand(handlerId1,
            subscriptionId1, command);
        
        var instructionId2 = sut.SubscribeToCommand(handlerId2,
            subscriptionId2, command);
        
        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId1,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId1,
                        subscriptionId1,
                        command
                    )),
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId2,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId2,
                        subscriptionId2,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, RegistrationId>(instructionId1, subscriptionId1),
                new KeyValuePair<InstructionId, RegistrationId>(instructionId2, subscriptionId2) 
            },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void SubscribeToCommandWhenAlreadySubscribeAndAcknowledgedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<RegistrationId>();
        var handlerId1 = _fixture.Create<RegistrationId>();
        var subscriptionId2 = _fixture.Create<RegistrationId>();
        var handlerId2 = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        sut.RegisterCommandHandler(handlerId1, new CountdownCompletionSource(1), new LoadFactor(1), (_, _) => Task.FromResult(new CommandResponse()));
        
        var instructionId1 = sut.SubscribeToCommand(handlerId1,
            subscriptionId1, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId1.ToString()
        });
        
        sut.RegisterCommandHandler(handlerId2, new CountdownCompletionSource(1), new LoadFactor(1), (_, _) => Task.FromResult(new CommandResponse()));

        var instructionId2 = sut.SubscribeToCommand(handlerId2,
            subscriptionId2, command);
        
        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId1,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId1,
                        subscriptionId1,
                        command
                    )),
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId2,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId2,
                        subscriptionId2,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, RegistrationId>(instructionId2, subscriptionId2) 
            },
            sut.SubscribeInstructions);
        Assert.Equal(
            new []
            {
                new KeyValuePair<CommandName,RegistrationId>(command, subscriptionId1)
            },
            sut.ActiveRegistrations);
    }
    
    [Fact]
    public void AcknowledgeWhenAlreadySubscribedAndAcknowledgedWithOtherCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId1 = _fixture.Create<RegistrationId>();
        var handlerId1 = _fixture.Create<RegistrationId>();
        var subscriptionId2 = _fixture.Create<RegistrationId>();
        var handlerId2 = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        sut.RegisterCommandHandler(handlerId1, new CountdownCompletionSource(1), new LoadFactor(1), (_, _) => Task.FromResult(new CommandResponse()));
        
        var instructionId1 = sut.SubscribeToCommand(handlerId1,
            subscriptionId1, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId1.ToString()
        });
        
        sut.RegisterCommandHandler(handlerId2, new CountdownCompletionSource(1), new LoadFactor(1), (_, _) => Task.FromResult(new CommandResponse()));
        
        var instructionId2 = sut.SubscribeToCommand(handlerId2,
            subscriptionId2, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = instructionId2.ToString()
        });
        
        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId2,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId2,
                        subscriptionId2,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.Equal(
            new []
            {
                new KeyValuePair<CommandName,RegistrationId>(command, subscriptionId2)
            },
            sut.ActiveRegistrations);
    }
    
    [Fact]
    public void AcknowledgeSubscribeToCommandWithUnknownInstructionIdHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        var instructionId = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);
        
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
        
        Assert.Empty(sut.ActiveRegistrations);
        Assert.Equal(
            new[] { 
                new KeyValuePair<InstructionId, RegistrationId>(instructionId, subscriptionId) 
            },
            sut.SubscribeInstructions);
    }
    
    [Fact]
    public void AcknowledgeSuccessfulSubscribeToCommandWithKnownInstructionIdHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (_, _) => Task.FromResult(new CommandResponse());
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource, loadFactor, handler);
        
        var instructionId = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId.ToString(),
            Error = new ErrorMessage
            {
                ErrorCode = ErrorCategory.All[Random.Shared.Next(0, ErrorCategory.All.Count)].ToString(),
                Message = _fixture.Create<string>(),
                Location = _fixture.Create<string>()
            }
        });
        
        Assert.Equal(
            new [] { new KeyValuePair<CommandName,RegistrationId>(command, subscriptionId)},
            sut.ActiveRegistrations);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.True(completionSource.Completion.IsCompletedSuccessfully);
    }
    
    [Fact]
    public void AcknowledgeUnsuccessfulSubscribeToCommandWithKnownInstructionIdHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (_, _) => Task.FromResult(new CommandResponse());
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, completionSource, loadFactor, handler);
        
        var instructionId = sut.SubscribeToCommand(handlerId,
            subscriptionId, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = false, 
            InstructionId = instructionId.ToString(),
            Error = new ErrorMessage
            {
                ErrorCode = ErrorCategory.All[Random.Shared.Next(0, ErrorCategory.All.Count)].ToString(),
                Message = _fixture.Create<string>(),
                Location = _fixture.Create<string>()
            }
        });
        
        Assert.Empty(sut.ActiveRegistrations);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.True(completionSource.Completion.IsFaulted);
    }
    
    [Fact]
    public void SubscribeToCommandMultipleTimesWithOutOfOrderAcknowledgementsHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        
        var command = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        Func<Command,CancellationToken,Task<CommandResponse>> handler = (_, _) => Task.FromResult(new CommandResponse());
        
        var subscriptionId1 = _fixture.Create<RegistrationId>();
        var handlerId1 = _fixture.Create<RegistrationId>();
        var completionSource1 = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId1, completionSource1, loadFactor, handler);
        
        var subscriptionId2 = _fixture.Create<RegistrationId>();
        var handlerId2 = _fixture.Create<RegistrationId>();
        var completionSource2 = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId2, completionSource2, loadFactor, handler);
        
        var instructionId1 = sut.SubscribeToCommand(handlerId1,
            subscriptionId1, command);
        
        Assert.Empty(sut.SupersededCommandRegistrations);
        
        var instructionId2 = sut.SubscribeToCommand(handlerId2,
            subscriptionId2, command);
        
        Assert.Equal(
            new [] { subscriptionId1 },
            sut.SupersededCommandRegistrations);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId2.ToString()
        });
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, 
            InstructionId = instructionId1.ToString()
        });
        
        Assert.Equal(
            new [] { new KeyValuePair<CommandName, RegistrationId>(command, subscriptionId2)},
            sut.ActiveRegistrations);
        Assert.Empty(sut.SubscribeInstructions);
        Assert.Empty(sut.SupersededCommandRegistrations);
        Assert.True(completionSource1.Completion.IsCompletedSuccessfully);
        Assert.True(completionSource2.Completion.IsCompletedSuccessfully);
    }
    
    [Fact]
    public void UnregisterUnknownCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<RegistrationId>();
        var completionSource = new CountdownCompletionSource(1);
        
        sut.UnregisterCommandHandler(handlerId, completionSource);

        Assert.Empty(sut.UnsubscribeCompletionSources);
        Assert.Empty(sut.UnsubscribeInstructions);
        Assert.Empty(sut.AllCommandHandlers);
    }
    
    [Fact]
    public void UnregisterKnownCommandHandlerHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        
        var handlerId = _fixture.Create<RegistrationId>();
        var completionSource = new CountdownCompletionSource(1);
        
        sut.RegisterCommandHandler(handlerId, new CountdownCompletionSource(1), new LoadFactor(1), (command, token) => Task.FromResult(new CommandResponse()));
        
        sut.UnregisterCommandHandler(handlerId, completionSource);

        Assert.Equal(
            new[] { new KeyValuePair<RegistrationId, CountdownCompletionSource>(handlerId, completionSource) },
            sut.UnsubscribeCompletionSources);
        Assert.Empty(sut.UnsubscribeInstructions);
        Assert.Empty(sut.AllCommandHandlers);
    }
    
    [Fact]
    public void UnsubscribeToCommandHasExpectedResult()
    {
        var sut = new CommandRegistrations(_clientIdentity, _clock);
        var subscriptionId = _fixture.Create<RegistrationId>();
        var handlerId = _fixture.Create<RegistrationId>();
        var command = _fixture.Create<CommandName>();
        
        sut.RegisterCommandHandler(handlerId, new CountdownCompletionSource(1), new LoadFactor(1), (command, token) => Task.FromResult(new CommandResponse()));
        
        var subscribeInstructionId = sut.SubscribeToCommand(handlerId, subscriptionId, command);
        
        sut.Acknowledge(new InstructionAck
        {
            Success = true, InstructionId = subscribeInstructionId.ToString()
        });
        
        sut.UnregisterCommandHandler(handlerId, new CountdownCompletionSource(1));
        
        var unsubscribeInstructionId = sut.UnsubscribeFromCommand(subscriptionId);

        Assert.True(unsubscribeInstructionId.HasValue);
        Assert.Equal(
            new[]
            {
                new KeyValuePair<RegistrationId, CommandRegistrations.RegisteredCommand>(subscriptionId,
                    new CommandRegistrations.RegisteredCommand(
                        handlerId,
                        subscriptionId,
                        command
                    ))
            }, sut.AllSubscriptions);
        Assert.Equal(
            new[] { new KeyValuePair<InstructionId, RegistrationId>(unsubscribeInstructionId!.Value, subscriptionId) },
            sut.UnsubscribeInstructions);
    }
}
