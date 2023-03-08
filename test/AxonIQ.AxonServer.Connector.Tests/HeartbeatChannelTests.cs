using AxonIQ.AxonServer.Connector.Tests.Framework;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

public class HeartbeatChannelTests
{
    //Note: Some of these tests may be somewhat on the slow side but that's
    //because the monitor uses a minimum interval of 1 second to check the pulse
    public class WhenChannelIsInitialized
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;

        public WhenChannelIsInitialized(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private HeartbeatChannel CreateSystemUnderTest(WritePlatformInboundInstruction writer)
        {
            return new HeartbeatChannel(writer,
                () => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = CreateSystemUnderTest(writer.Write);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = CreateSystemUnderTest(writer.Write);
            
            await sut.Disable();
        
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = CreateSystemUnderTest(writer.Write);
            
            await sut.Pause();
        
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = CreateSystemUnderTest(writer.Write);
            
            await sut.Resume();
        
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }

    public class WhenChannelIsDisabled
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;
    
        public WhenChannelIsDisabled(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private async Task<HeartbeatChannel> CreateSystemUnderTest(WritePlatformInboundInstruction writer)
        {
            var sut = new HeartbeatChannel(writer,
                () => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
            await sut.Disable();
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = await CreateSystemUnderTest(writer.Write);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
    
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Disable();
    
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Pause();
    
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(1);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Resume();
    
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }
    
    public class WhenChannelIsEnabled
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;
    
        public WhenChannelIsEnabled(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private async Task<HeartbeatChannel> CreateSystemUnderTest(WritePlatformInboundInstruction writer, OnHeartbeatMissed onHeartbeatMissed)
        {
            var sut = new HeartbeatChannel(writer,
                onHeartbeatMissed,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
            await sut.Enable(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return sut;
        }
    
        [Fact]
        public async Task HeartbeatMissedGetsTriggeredWhenHeartbeatIsNeverAcknowledged()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnHeartbeatMissed missed = () => { source.TrySetResult(); return ValueTask.CompletedTask; };
            WritePlatformInboundInstruction writer = _ => ValueTask.CompletedTask; 
            await using var sut = await CreateSystemUnderTest(writer, missed);
            
            Assert.True(source.Task.Wait(TimeSpan.FromSeconds(1)));
        }
        
        [Fact]
        public async Task HeartbeatMissedGetsTriggeredWhenHeartbeatIsNotAcknowledgedInTime()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnHeartbeatMissed missed = () => { source.TrySetResult(); return ValueTask.CompletedTask; };
            var instructions = new List<PlatformInboundInstruction>();
            WritePlatformInboundInstruction writer = instruction =>
            {
                instructions.Add(instruction);
                return ValueTask.CompletedTask;
            };
            await using(var sut = await CreateSystemUnderTest(writer, missed)) {

                _logger.LogDebug("Waiting 500ms");
                await Task.Delay(TimeSpan.FromMilliseconds(500));
                _logger.LogDebug("Waited 500ms");

                foreach (var instruction in instructions.ToArray())
                {
                    _logger.LogDebug("Sending ReceiveClientHeartbeatAcknowledgement");
                    await sut.ReceiveClientHeartbeatAcknowledgement(new InstructionAck
                        { InstructionId = instruction.InstructionId, Success = true });
                    _logger.LogDebug("Sent ReceiveClientHeartbeatAcknowledgement");
                }

                _logger.LogDebug("Waiting for heartbeat to be missed");
                Assert.True(source.Task.Wait(TimeSpan.FromSeconds(1)));
            }
        }
        
        [Fact]
        public async Task HeartbeatMissedDoesNotGetTriggeredWhenHeartbeatIsAcknowledgedInTime()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnHeartbeatMissed missed = () => { source.TrySetResult(); return ValueTask.CompletedTask; };
            var instructions = new List<PlatformInboundInstruction>();
            WritePlatformInboundInstruction writer = instruction =>
            {
                instructions.Add(instruction);
                return ValueTask.CompletedTask;
            };
            await using var sut = await CreateSystemUnderTest(writer, missed);
        
            foreach (var instruction in instructions)
            {
                await sut.ReceiveClientHeartbeatAcknowledgement(new InstructionAck{ InstructionId = instruction.InstructionId, Success = true });    
            }
            
            Assert.False(source.Task.Wait(TimeSpan.FromMilliseconds(500)));
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            OnHeartbeatMissed missed = () => ValueTask.CompletedTask;
            var writer = new WrittenPlatformInboundInstructionCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            OnHeartbeatMissed missed = () => ValueTask.CompletedTask;
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
        
            await sut.Disable();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            OnHeartbeatMissed missed = () => ValueTask.CompletedTask;
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
        
            await sut.Pause();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            OnHeartbeatMissed missed = () => ValueTask.CompletedTask;
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
        
            await sut.Resume();
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }
    
    public class WhenChannelIsPaused
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;
    
        public WhenChannelIsPaused(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private async Task<HeartbeatChannel> CreateSystemUnderTest(WritePlatformInboundInstruction writer)
        {
            var sut = new HeartbeatChannel(writer,
                () => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
            await sut.Enable(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            await sut.Pause();
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(writer.Write);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Disable();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Pause();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(2);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Resume();
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }
    
    public class WhenChannelIsResumed
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;
    
        public WhenChannelIsResumed(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private async Task<HeartbeatChannel> CreateSystemUnderTest(WritePlatformInboundInstruction writer)
        {
            var sut = new HeartbeatChannel(writer,
                () => ValueTask.CompletedTask,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            await sut.Pause();
            await sut.Resume();
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(3); // 1 count for the initial enable, 1 for the resume, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(writer.Write);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(3);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Disable();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(3);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Pause();
            
            Assert.False(writer.Completed.Wait(TimeSpan.FromSeconds(2)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var writer = new WrittenPlatformInboundInstructionCountdown(3);
            await using var sut = await CreateSystemUnderTest(writer.Write);
    
            await sut.Resume();
            
            Assert.True(writer.Completed.Wait(TimeSpan.FromSeconds(2)));
        }
    }
    
    public class WhenClientHeartbeatIsNotAcknowledgedInTime
    {
        private readonly TestOutputHelperLogger<HeartbeatChannel> _logger;
        private readonly TestOutputHelperLogger<Scheduler> _schedulerLogger;
    
        public WhenClientHeartbeatIsNotAcknowledgedInTime(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatChannel>(output);
            _schedulerLogger = new TestOutputHelperLogger<Scheduler>(output);
        }
        
        private async Task<HeartbeatChannel> CreateSystemUnderTest(WritePlatformInboundInstruction writer, OnHeartbeatMissed onHeartbeatMissed)
        {
            var sut = new HeartbeatChannel(writer,
                onHeartbeatMissed,
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(2),
                new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _schedulerLogger),
                _logger);
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            return sut;
        }
        
        [Fact]
        public async Task NeverAcknowledgingHasExpectedResult()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnHeartbeatMissed missed = () => { source.TrySetResult(); return ValueTask.CompletedTask; };
            var writer = new CaptureClientHeartbeatInstructionWriter();
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
    
            
            await source.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        
        [Fact]
        public async Task AcknowledgingTooLateHasExpectedResult()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            OnHeartbeatMissed missed = () => { source.TrySetResult(); return ValueTask.CompletedTask; };
            var writer = new CaptureClientHeartbeatInstructionWriter();
            await using var sut = await CreateSystemUnderTest(writer.Write, missed);
            
            //Allow the heartbeat to be missed 
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            Assert.NotNull(writer.Instruction);
            
            //Respond after the above delay
            await sut.ReceiveClientHeartbeatAcknowledgement(new InstructionAck
            {
                InstructionId = writer.Instruction.InstructionId,
                Success = true
            });
            
            await source.Task;
        }
    }

    private class WrittenPlatformInboundInstructionCountdown
    {
        private int _counter;
        private readonly TaskCompletionSource _source;
        

        public WrittenPlatformInboundInstructionCountdown(int from)
        {
            _counter = from;
            _source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public ValueTask Write(PlatformInboundInstruction instruction)
        {
            if (Interlocked.Decrement(ref _counter) == 0)
            {
                _source.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        public Task Completed => _source.Task;
    }

    private class CaptureClientHeartbeatInstructionWriter
    {
        public PlatformInboundInstruction? Instruction { get; private set; }

        public ValueTask Write(PlatformInboundInstruction instruction)
        {
            Instruction ??= instruction;
            return ValueTask.CompletedTask;
        }
    }
}
