using AxonIQ.AxonServer.Connector.Tests.Framework;
using Io.Axoniq.Axonserver.Grpc;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

public class HearbeatMonitorTests
{
    //Note: Some of these tests may be somewhat on the slow side but that's
    //because the monitor uses a minimum interval of 1 second to check the pulse
    public class WhenMonitorIsInitialized
    {
        private TestOutputHelperLogger<HeartbeatMonitor> _logger;

        public WhenMonitorIsInitialized(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatMonitor>(output);
        }
        
        private HeartbeatMonitor CreateSystemUnderTest(SendHeartbeat sender)
        {
            return new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, TimeSpan.Zero, _logger);
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Disable();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Pause();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Resume();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }

    public class WhenMonitorIsDisabled
    {
        private TestOutputHelperLogger<HeartbeatMonitor> _logger;

        public WhenMonitorIsDisabled(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatMonitor>(output);
        }
        
        private async Task<HeartbeatMonitor> CreateSystemUnderTest(SendHeartbeat sender)
        {
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, _logger);
            await sut.Disable();
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();

            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }

    public class WhenMonitorIsEnabled
    {
        private readonly ITestOutputHelper _output;
        private TestOutputHelperLogger<HeartbeatMonitor> _logger;

        public WhenMonitorIsEnabled(ITestOutputHelper output)
        {
            _output = output;
            _logger = new TestOutputHelperLogger<HeartbeatMonitor>(output);
        }
        
        private async Task<HeartbeatMonitor> CreateSystemUnderTest(SendHeartbeat sender)
        {
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, TimeSpan.Zero, _logger);
            await sut.Enable(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return sut;
        }

        [Fact]
        public async Task HeartbeatMissedGetsTriggeredWhenHeartbeatIsNeverAcknowledged()
        {
            SendHeartbeat sender = (_, _) => ValueTask.CompletedTask; 
            await using var sut = await CreateSystemUnderTest(sender);

            var source = new TaskCompletionSource();
            sut.HeartbeatMissed += (_, _) =>
            {
                source.TrySetResult();
            };
            
            Assert.True(source.Task.Wait(TimeSpan.FromSeconds(1)));
        }
        
        [Fact]
        public async Task HeartbeatMissedGetsTriggeredWhenHeartbeatIsNotAcknowledgedInTime()
        {
            var responders = new List<ReceiveHeartbeatAcknowledgement>();
            SendHeartbeat sender = (responder, _) =>
            {
                responders.Add(responder);
                return ValueTask.CompletedTask;
            };
            await using var sut = await CreateSystemUnderTest(sender);

            var source = new TaskCompletionSource(false);
            sut.HeartbeatMissed += (_, _) =>
            {
                source.TrySetResult();
            };

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            foreach (var responder in responders)
            {
                await responder(new InstructionAck{ Success = true });    
            }
            
            Assert.True(source.Task.Wait(TimeSpan.FromSeconds(1)));
        }
        
        [Fact]
        public async Task HeartbeatMissedDoesNotGetTriggeredWhenHeartbeatIsAcknowledgedInTime()
        {
            var responders = new List<ReceiveHeartbeatAcknowledgement>();
            SendHeartbeat sender = (responder, _) =>
            {
                responders.Add(responder);
                return ValueTask.CompletedTask;
            };
            await using var sut = await CreateSystemUnderTest(sender);

            var source = new TaskCompletionSource();
            sut.HeartbeatMissed += (_, _) =>
            {
                source.TrySetResult();
            };
            
            foreach (var responder in responders)
            {
                await responder(new InstructionAck{ Success = true });    
            }
            
            Assert.False(source.Task.Wait(TimeSpan.FromMilliseconds(250)));
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }

    public class WhenMonitorIsPaused
    {
        private TestOutputHelperLogger<HeartbeatMonitor> _logger;

        public WhenMonitorIsPaused(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatMonitor>(output);
        }
        
        private async Task<HeartbeatMonitor> CreateSystemUnderTest(SendHeartbeat sender)
        {
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, TimeSpan.Zero, _logger);
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            await sut.Pause();
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
    }

    public class WhenMonitorIsResumed
    {
        private TestOutputHelperLogger<HeartbeatMonitor> _logger;

        public WhenMonitorIsResumed(ITestOutputHelper output)
        {
            _logger = new TestOutputHelperLogger<HeartbeatMonitor>(output);
        }
        
        private async Task<HeartbeatMonitor> CreateSystemUnderTest(SendHeartbeat sender)
        {
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, _logger);
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
            var sender = new SentHeartbeatCountdown(3); // 1 count for the initial enable, 1 for the resume, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Completed.Wait(TimeSpan.FromSeconds(2)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.True(sender.Completed.Wait(TimeSpan.FromSeconds(2)));
        }
    }
    
    public class WhenHeartbeatIsNotAcknowledgedInTime
    {
        
    }
    
    public class SentHeartbeatCountdown
    {
        private int _counter;
        public TaskCompletionSource _source;
        

        public SentHeartbeatCountdown(int from)
        {
            _counter = from;
            _source = new TaskCompletionSource();
        }

        public ValueTask SendHeartbeat(ReceiveHeartbeatAcknowledgement responder, TimeSpan timeout)
        {
            if (Interlocked.Decrement(ref _counter) == 0)
            {
                _source.TrySetResult();
            }
            return ValueTask.CompletedTask;
        }

        public Task Completed => _source.Task;
    }
}