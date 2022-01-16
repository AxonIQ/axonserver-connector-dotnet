using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging;
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
            return new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, _logger);
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Disable();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Pause();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Resume();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
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

            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(1);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();

            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
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
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, _logger);
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
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
            var sut = new HeartbeatMonitor(sender, () => DateTimeOffset.UtcNow, _logger);
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await sut.Pause();
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2); // 1 count for the initial enable, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(2);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
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
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await sut.Pause();
            await sut.Resume();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            return sut;
        }
        
        [Fact]
        public async Task EnableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3); // 1 count for the initial enable, 1 for the resume, 1 for the second enable
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);
            
            await sut.Enable(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task DisableHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Disable();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task PauseHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Pause();
            
            Assert.False(sender.Signal.Wait(TimeSpan.FromSeconds(2)));
        }
        
        [Fact]
        public async Task ResumeHasExpectedResult()
        {
            var sender = new SentHeartbeatCountdown(3);
            await using var sut = await CreateSystemUnderTest(sender.SendHeartbeat);

            await sut.Resume();
            
            Assert.True(sender.Signal.Wait(TimeSpan.FromSeconds(2)));
        }
    }

    public class WhenHeartbeatIsNeverAcknowledged
    {
        
    }
    
    public class WhenHeartbeatIsNotAcknowledgedInTime
    {
        
    }
    
    public class SentHeartbeatCountdown
    {
        public CountdownEvent Signal { get; }

        public SentHeartbeatCountdown(int from)
        {
            Signal = new CountdownEvent(from);
        }

        public ValueTask SendHeartbeat(ReceiveHeartbeatAcknowledgement responder)
        {
            Signal.Signal();
            return ValueTask.CompletedTask;
        }
    }
}