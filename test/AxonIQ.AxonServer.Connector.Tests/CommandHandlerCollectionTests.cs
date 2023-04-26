using AutoFixture;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Command;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CommandHandlerCollectionTests
{
    private readonly Fixture _fixture;

    public CommandHandlerCollectionTests()
    {
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeLoadFactor();
        _fixture.CustomizeRegisteredCommandId();
    }

    private CommandHandlerCollection CreateSystemUnderTest(Func<DateTimeOffset>? clock = default)
    {
        return new CommandHandlerCollection(
            _fixture.Create<ClientIdentity>(),
            clock ?? (() => DateTimeOffset.UtcNow)
        );
    }
    
    [Fact]
    public void RegisterCommandHandlerHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        
        // Command handler can not be resolved by name because we've not subscribed any commands
        Assert.False(sut.TryGetCommandHandler(name, out _));
        
        // 1 command registered at this point
        Assert.Equal(new[] { id }, sut.RegisteredCommands);
    }
    
    [Fact]
    public void BeginSubscribeToCommandInstructionWithUnknownCommandIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();
        
        Assert.Throws<InvalidOperationException>(() => sut.BeginSubscribeToCommandInstruction(RegisteredCommandId.New()));
    }
    
    [Fact]
    public void BeginSubscribeToCommandInstructionWithKnownCommandIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        var result = sut.BeginSubscribeToCommandInstruction(id);
        
        Assert.NotEmpty(result.InstructionId);
        Assert.NotNull(result.Subscribe);
        Assert.Equal(sut.ClientIdentity.ClientInstanceId.ToString(), result.Subscribe.ClientId);
        Assert.Equal(sut.ClientIdentity.ComponentName.ToString(), result.Subscribe.ComponentName);
        Assert.Equal(name.ToString(), result.Subscribe.Command);
        Assert.Equal(loadFactor.ToInt32(), result.Subscribe.LoadFactor);
        Assert.NotEmpty(result.Subscribe.MessageId);
        
        // Command handler can not be resolved by name because we've not subscribed any commands
        Assert.False(sut.TryGetCommandHandler(name, out _));
        
        // 1 command registered at this point
        Assert.Equal(new[] { id }, sut.RegisteredCommands);
    }
    
    [Fact]
    public void TryCompleteInstructionForSubscribeToCommandHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        var result = sut.BeginSubscribeToCommandInstruction(id);
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(new InstructionAck { InstructionId = result.InstructionId, Success = true}));
        
        Assert.True(sut.TryGetCommandHandler(name, out var resolvedHandler));
        Assert.Same(handler, resolvedHandler);
        Assert.Equal(new[] { id }, sut.RegisteredCommands);
    }
    
    [Fact]
    public void TryCompleteInstructionForSubscribeToSameCommandNameHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredCommandId>();
        var id2 = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id1, name, loadFactor, handler);
        sut.RegisterCommandHandler(id2, name, loadFactor, handler);
        var result1 = sut.BeginSubscribeToCommandInstruction(id1);
        var result2 = sut.BeginSubscribeToCommandInstruction(id2);
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(new InstructionAck { InstructionId = result1.InstructionId, Success = true}));
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(new InstructionAck { InstructionId = result2.InstructionId, Success = true}));
        
        Assert.True(sut.TryGetCommandHandler(name, out var resolvedHandler));
        Assert.Same(handler, resolvedHandler);
        Assert.Equal(new[] { id1, id2 }, sut.RegisteredCommands);
    }
    
    [Fact]
    public void BeginUnsubscribeFromCommandInstructionWithKnownCommandIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        var result = sut.BeginUnsubscribeFromCommandInstruction(id);
        
        Assert.NotEmpty(result.InstructionId);
        Assert.NotNull(result.Unsubscribe);
        Assert.Equal(sut.ClientIdentity.ClientInstanceId.ToString(), result.Unsubscribe.ClientId);
        Assert.Equal(sut.ClientIdentity.ComponentName.ToString(), result.Unsubscribe.ComponentName);
        Assert.Equal(name.ToString(), result.Unsubscribe.Command);
        Assert.Equal(default, result.Unsubscribe.LoadFactor);
        Assert.NotEmpty(result.Unsubscribe.MessageId);
        
        // Command handler can not be resolved by name because we've not subscribed any commands
        Assert.False(sut.TryGetCommandHandler(name, out _));
        
        // 1 registered command at this point
        Assert.Equal(new [] { id }, sut.RegisteredCommands);
    }
    
    [Fact]
    public void BeginUnsubscribeFromCommandInstructionWithUnknownCommandIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        Assert.Throws<InvalidOperationException>(() => sut.BeginUnsubscribeFromCommandInstruction(RegisteredCommandId.New()));
    }
    
    [Fact]
    public void TryCompleteSubscribeToCommandInstructionWithUnknownAcknowledgedInstructionIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        Assert.False(sut.TryCompleteSubscribeToCommandInstruction(new InstructionAck { InstructionId = InstructionId.New().ToString(), Success = true}));
    }
    
    [Fact]
    public void TryCompleteUnsubscribeFromCommandInstructionWithUnknownAcknowledgedInstructionIdHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        Assert.False(sut.TryCompleteUnsubscribeFromCommandInstruction(new InstructionAck { InstructionId = InstructionId.New().ToString(), Success = true}));
    }
    
    [Fact]
    public void TryCompleteInstructionForUnsubscribeFromUnsubscribedCommandHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        var result = sut.BeginUnsubscribeFromCommandInstruction(id);
        Assert.True(sut.TryCompleteUnsubscribeFromCommandInstruction(new InstructionAck { InstructionId = result.InstructionId, Success = true}));
        
        Assert.False(sut.TryGetCommandHandler(name, out var resolvedHandler));
        Assert.Null(resolvedHandler);
        Assert.Empty(sut.RegisteredCommands);
    }
    
    [Fact]
    public void TryCompleteInstructionForUnsubscribeFromSubscribedCommandHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id, name, loadFactor, handler);
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(
            new InstructionAck { InstructionId = sut.BeginSubscribeToCommandInstruction(id).InstructionId, Success = true}));
        
        Assert.True(sut.TryGetCommandHandler(name, out var resolvedHandler1));
        Assert.Same(handler, resolvedHandler1);
        Assert.Equal(new[] { id }, sut.RegisteredCommands);
        
        var result = sut.BeginUnsubscribeFromCommandInstruction(id);
        Assert.True(sut.TryCompleteUnsubscribeFromCommandInstruction(
            new InstructionAck { InstructionId = result.InstructionId, Success = true }));
        
        Assert.False(sut.TryGetCommandHandler(name, out var resolvedHandler2));
        Assert.Null(resolvedHandler2);
        Assert.Empty(sut.RegisteredCommands);
    }
    
    [Fact]
    public void TryCompleteInstructionForUnsubscribeFromSupersededCommandHasExpectedResult()
    {
        var sut = CreateSystemUnderTest();

        var id1 = _fixture.Create<RegisteredCommandId>();
        var id2 = _fixture.Create<RegisteredCommandId>();
        var name = _fixture.Create<CommandName>();
        var loadFactor = _fixture.Create<LoadFactor>();
        var handler1 = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());
        var handler2 = (Command command, CancellationToken ct) => Task.FromResult(new CommandResponse());

        sut.RegisterCommandHandler(id1, name, loadFactor, handler1);
        sut.RegisterCommandHandler(id2, name, loadFactor, handler2);
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(
            new InstructionAck { InstructionId = sut.BeginSubscribeToCommandInstruction(id1).InstructionId, Success = true}));
        Assert.True(sut.TryCompleteSubscribeToCommandInstruction(
            new InstructionAck { InstructionId = sut.BeginSubscribeToCommandInstruction(id2).InstructionId, Success = true}));
        
        Assert.True(sut.TryGetCommandHandler(name, out var resolvedHandler1));
        Assert.Same(handler2, resolvedHandler1);
        Assert.Equal(new[] { id1, id2 }, sut.RegisteredCommands);
        
        var result = sut.BeginUnsubscribeFromCommandInstruction(id1);
        Assert.True(sut.TryCompleteUnsubscribeFromCommandInstruction(
            new InstructionAck { InstructionId = result.InstructionId, Success = true }));
        
        Assert.True(sut.TryGetCommandHandler(name, out var resolvedHandler2));
        Assert.Same(handler2, resolvedHandler2);
        Assert.Equal(new[] { id2 }, sut.RegisteredCommands);
    }
}