using System.Threading.Channels;
using AxonIQ.AxonServer.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class HeartbeatMonitor : IAsyncDisposable
{
    private readonly SendHeartbeat _sender;
    private readonly HeartbeatMissed _missed;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ILogger<HeartbeatMonitor> _logger;

    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    private readonly Timer _timer;
    
    private readonly LeaderClock _leaderClock;
    private readonly FollowerClock _followerClock;

    public HeartbeatMonitor(SendHeartbeat sender, HeartbeatMissed missed, Func<DateTimeOffset> clock, ILogger<HeartbeatMonitor> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _missed = missed ?? throw new ArgumentNullException(nameof(missed));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        _leaderClock = new LeaderClock();
        _followerClock = new FollowerClock();
        _timer = new Timer(
            logicalClock =>
            {
                if (logicalClock != null)
                {
                    var message = new Protocol.Check(
                        clock(),
                        ((FollowerClock)logicalClock).LogicalTime);
                    if (!_inbox.Writer.TryWrite(message))
                    {
                        _logger.LogDebug("Could not tell the monitor to check the heartbeat because the inbox refused");
                    }
                }
                else
                {
                    _logger.LogDebug("Could not tell the monitor to check the heartbeat because the clock is missing");
                }
            }, _followerClock, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        State state = new State.Disabled(_leaderClock.LogicalTime);
        while (await _inbox.Reader.WaitToReadAsync(ct))
        {
            while (_inbox.Reader.TryRead(out var message))
            {
                switch (message)
                {
                    case Protocol.Enable enable:
                        switch (state)
                        {
                            case State.Disabled:
                            case State.Paused:
                            case State.Enabled:
                            {
                                var wallTime = _clock();
                                state = new State.Enabled(
                                    enable.Interval, 
                                    enable.Timeout,
                                    wallTime,
                                    wallTime.Add(enable.Timeout),
                                    enable.LogicalTime);
                                _followerClock.Follow(enable.LogicalTime);
                                _timer.Change(TimeSpan.Zero, enable.Interval);
                                break;
                            }
                        }
                        break;
                    case Protocol.Check check:
                        switch (state)
                        {
                            case State.Enabled enabled:
                            {
                                if (check.LogicalTime == enabled.LogicalTime)
                                {
                                    var wallTime = _clock();
                                    if (enabled.NextHeartbeatCheckDeadlineAt < wallTime)
                                    {
                                        _logger.LogInformation("Did not receive heartbeat acknowledgement within {Timeout}ms", Convert.ToInt32(enabled.Timeout.TotalMilliseconds));
                                        await _missed();
                                        enabled = enabled with { NextHeartbeatCheckDeadlineAt = wallTime.Add(enabled.Timeout) };
                                    }

                                    if (enabled.NextHeartbeatCheckAt < wallTime)
                                    {
                                        await _sender(ack => 
                                            ack.Success 
                                                ? _inbox.Writer.WriteAsync(new Protocol.CheckSucceeded(enabled.LogicalTime), ct) 
                                                : _inbox.Writer.WriteAsync(new Protocol.CheckFailed(ack.Error, enabled.LogicalTime), ct));
                                        enabled = enabled with { NextHeartbeatCheckAt = wallTime.Add(enabled.Interval) };
                                    }

                                    state = enabled;
                                }
                                else
                                {
                                    _logger.LogDebug("Could not check heartbeat because the check request is too old");
                                }
                                break;
                            }
                            default:
                                _logger.LogDebug("Could not check heartbeat because the monitor is not enabled");
                                break;
                        }
                        break;
                    case Protocol.Disable disable:
                        state = new State.Disabled(disable.LogicalTime);
                        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                        break;
                    case Protocol.Pause pause:
                        switch (state)
                        {
                            case State.Enabled enabled:
                            {
                                state = new State.Paused(enabled.Interval, enabled.Timeout, pause.LogicalTime);
                                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                break;
                            }
                            case State.Paused:
                                break;
                            case State.Disabled:
                                _logger.LogDebug("Could not pause the heartbeat monitor because the monitor is disabled");
                                break;
                        }
                        break;
                    case Protocol.Resume resume:
                        switch (state)
                        {
                            case State.Paused paused:
                            {
                                var wallTime = _clock();
                                state = new State.Enabled(
                                    paused.Interval, 
                                    paused.Timeout,
                                    wallTime,
                                    wallTime.Add(paused.Timeout),
                                    resume.LogicalTime);
                                _timer.Change(TimeSpan.Zero, paused.Interval);
                                break;
                            }
                            case State.Enabled:
                                break;
                            case State.Disabled:
                                _logger.LogDebug("Could not pause the heartbeat monitor because the monitor is disabled");
                                break;
                        }
                        break;
                    //TODO: Handle CheckFailed, CheckSucceeded, ReceiveServerHeartbeat
                }
            }
        }
    }

    private record Protocol
    {
        public record Enable(TimeSpan Interval, TimeSpan Timeout, long LogicalTime) : Protocol;
        public record Check(DateTimeOffset Instant, long LogicalTime) : Protocol;
        public record Disable(long LogicalTime) : Protocol;
        public record Pause(long LogicalTime) : Protocol;
        public record Resume(long LogicalTime) : Protocol;
        public record CheckSucceeded(long LogicalTime) : Protocol;
        public record CheckFailed(ErrorMessage Error, long LogicalTime) : Protocol;
    }

    private record State
    {
        public record Disabled(long LogicalTime) : State;
        public record Enabled(TimeSpan Interval, TimeSpan Timeout, DateTimeOffset NextHeartbeatCheckAt, DateTimeOffset NextHeartbeatCheckDeadlineAt, long LogicalTime) : State;
        public record Paused(TimeSpan Interval, TimeSpan Timeout, long LogicalTime) : State;
    }

    private class LeaderClock
    {
        private long _logicalTime;

        public LeaderClock()
        {
            _logicalTime = 0L;
        }

        public long Next()
        {
            return Interlocked.Increment(ref _logicalTime);
        }

        public long LogicalTime => Interlocked.Read(ref _logicalTime); 
    }

    private class FollowerClock
    {
        private long _logicalTime;

        public FollowerClock()
        {
            _logicalTime = 0L;
        }

        public long LogicalTime => Interlocked.Read(ref _logicalTime);
        
        public void Follow(long ticks)
        {
            Interlocked.Exchange(ref _logicalTime, ticks);
        }
    }

    public async Task Enable(TimeSpan interval, TimeSpan timeout)
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Enable(
                TimeSpanMath.Max(interval, TimeSpan.FromSeconds(1)),
                timeout,
                _leaderClock.Next()
            )
        );
    }
    
    public async Task Disable()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Disable(
                _leaderClock.Next()
            )
        );
    }
    
    public async Task Pause()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Pause(
                _leaderClock.Next()
            )
        );
    }
    
    public async Task Resume()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.Resume(
                _leaderClock.Next()
            )
        );
    }
    
    // TODO: Receive Server Heartbeat
    
    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion;
        await _protocol;
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}