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
        var signal = new ManualResetEventSlim(false);
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            signal.Set();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromSeconds(10));
        
        await sut.Receive(ack);
        
        Assert.True(signal.Wait(TimeSpan.FromMilliseconds(50)));
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
        var signal = new ManualResetEventSlim(false);
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            signal.Set();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromMilliseconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        await sut.Receive(ack);
        
        Assert.True(signal.Wait(TimeSpan.FromMilliseconds(50)));
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
        var signal = new ManualResetEventSlim(false);
        ReceiveHeartbeatAcknowledgement responder = _ =>
        {
            signal.Set();
            return ValueTask.CompletedTask;
        };
        await sut.Send(responder, TimeSpan.FromMilliseconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await sut.Receive(ack);
        
        Assert.False(signal.Wait(TimeSpan.FromMilliseconds(50)));
    }
}