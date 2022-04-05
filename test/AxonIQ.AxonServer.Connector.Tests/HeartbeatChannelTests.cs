using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Grpc;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

public class HeartbeatChannelTests
{
    private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;

    public HeartbeatChannelTests(ITestOutputHelper output)
    {
        _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
    }

    private HeartbeatChannel CreateSystemUnderTest(WritePlatformInboundInstruction writer, TimeSpan? purgeInterval = default)
    {
        return new HeartbeatChannel(() => DateTimeOffset.UtcNow, writer, purgeInterval.GetValueOrDefault(TimeSpan.FromSeconds(1)), _logger);
    }

    [Fact]
    public async Task ReceiveAcknowledgementForUnknownInstructionHasExpectedResult()
    {
        await using (var sut = CreateSystemUnderTest(_ => ValueTask.CompletedTask))
        {
            await sut.Receive(new InstructionAck { InstructionId = Guid.NewGuid().ToString("D") });
        }
    }
    
    [Fact]
    public async Task ReceiveAcknowledgementForKnownInstructionInTimeHasExpectedResult()
    {
        var ack = new InstructionAck();
        await using var sut = CreateSystemUnderTest(instruction =>
            {
                ack.InstructionId = instruction.InstructionId;
                return ValueTask.CompletedTask;
            } 
        );
        var source = new TaskCompletionSource();
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromSeconds(10));
        
        await sut.Receive(ack);
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(50)));
    }
    
    [Fact]
    public async Task ReceiveAcknowledgementForKnownInstructionOutOfTimeButBeforePurgeHasExpectedResult()
    {
        var ack = new InstructionAck();
        await using var sut = CreateSystemUnderTest(instruction =>
            {
                ack.InstructionId = instruction.InstructionId;
                return ValueTask.CompletedTask;
            }, TimeSpan.FromHours(1) 
        );
        var source = new TaskCompletionSource();
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromMilliseconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        await sut.Receive(ack);
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(50)));
    }
    
    [Fact]
    public async Task ReceiveAcknowledgementForKnownInstructionOutOfTimeButAfterPurgeHasExpectedResult()
    {
        var ack = new InstructionAck();
        await using var sut = CreateSystemUnderTest(instruction =>
            {
                ack.InstructionId = instruction.InstructionId;
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(250) 
        );
        var source = new TaskCompletionSource();
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromMilliseconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await sut.Receive(ack);
        
        Assert.False(source.Task.Wait(TimeSpan.FromMilliseconds(50)));
    }
}