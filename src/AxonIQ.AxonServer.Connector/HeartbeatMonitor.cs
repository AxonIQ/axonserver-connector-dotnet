using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class HeartbeatMonitor : IAsyncDisposable
{
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromSeconds(1.0);
    
    private readonly SendHeartbeat _sender;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _minimumCheckInterval;
    private readonly ILogger<HeartbeatMonitor> _logger;

    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    private readonly Timer _timer;
    
    private readonly LeaderClock _leaderClock;
    private readonly FollowerClock _followerClock;

    public HeartbeatMonitor(SendHeartbeat sender, Func<DateTimeOffset> clock, ILogger<HeartbeatMonitor> logger)
        : this(sender, clock, MinimumCheckInterval, logger)
    {
    }

    internal HeartbeatMonitor(SendHeartbeat sender, Func<DateTimeOffset> clock, TimeSpan minimumCheckInterval, ILogger<HeartbeatMonitor> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _minimumCheckInterval = minimumCheckInterval;
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
        try
        {
            State state = new State.Disabled(_leaderClock.LogicalTime);
            while (!ct.IsCancellationRequested && await _inbox.Reader.WaitToReadAsync(ct))
            {
                while (_inbox.Reader.TryRead(out var message))
                {
                    var wallTime = _clock();
                    _logger.LogDebug("Began {Message} when {State}", message.ToString(), state.ToString());
                    switch (message)
                    {
                        case Protocol.Enable enable:
                            // Note: Enable always causes a state transition regardless of the state we are in 
                            _timer.Change(TimeSpan.Zero, enable.Interval);
                            state = new State.Enabled(
                                enable.Interval,
                                enable.Timeout,
                                wallTime,
                                wallTime.Add(enable.Timeout),
                                enable.LogicalTime);
                            _followerClock.Follow(enable.LogicalTime);
                            _timer.Change(TimeSpan.Zero, enable.Interval);

                            break;
                        case Protocol.Check check:
                            switch (state)
                            {
                                // Note: Only check when enabled and the timer that triggered a check is using the latest configuration 
                                case State.Enabled enabled when check.LogicalTime == enabled.LogicalTime:
                                {
                                    if (enabled.NextHeartbeatCheckDeadlineAt < wallTime)
                                    {
                                        _logger.LogInformation(
                                            "Did not receive heartbeat acknowledgement within {Timeout}ms",
                                            Convert.ToInt32(enabled.Timeout.TotalMilliseconds));
                                        OnHeartbeatMissed();
                                        enabled = enabled with
                                        {
                                            NextHeartbeatCheckDeadlineAt = wallTime.Add(enabled.Timeout)
                                        };
                                        _logger.LogDebug(
                                            "Extended heartbeat deadline to {NewDeadLine}",
                                            enabled.NextHeartbeatCheckDeadlineAt);
                                    }

                                    if (enabled.NextHeartbeatCheckAt < wallTime)
                                    {
                                        await _sender(ack =>
                                            ack.Success
                                                ? _inbox.Writer.WriteAsync(
                                                    new Protocol.CheckSucceeded(enabled.LogicalTime), ct)
                                                : _inbox.Writer.WriteAsync(
                                                    new Protocol.CheckFailed(ack.Error, enabled.LogicalTime), ct),
                                            enabled.Timeout);
                                        enabled = enabled with
                                        {
                                            NextHeartbeatCheckAt = wallTime.Add(enabled.Interval)
                                        };

                                        _logger.LogDebug(
                                            "Extended heartbeat check to {NewCheck}",
                                            enabled.NextHeartbeatCheckAt);
                                    }

                                    state = enabled;

                                    break;
                                }
                                case State.Enabled enabled when check.LogicalTime != enabled.LogicalTime:
                                    _logger.LogDebug("Could not check heartbeat because the check request is outdated");
                                    break;
                                default:
                                    _logger.LogDebug("Could not check heartbeat because the monitor is not enabled");
                                    break;
                            }

                            break;
                        case Protocol.Disable disable:
                            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                            _followerClock.Follow(disable.LogicalTime);
                            state = new State.Disabled(disable.LogicalTime);
                            break;
                        case Protocol.Pause pause:
                            switch (state)
                            {
                                case State.Enabled enabled:
                                {
                                    _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                    _followerClock.Follow(pause.LogicalTime);
                                    state = new State.Paused(enabled.Interval, enabled.Timeout, pause.LogicalTime);
                                    break;
                                }
                                case State.Paused:
                                    _followerClock.Follow(pause.LogicalTime);
                                    _logger.LogDebug(
                                        "Could not pause the heartbeat monitor because the monitor is already paused");
                                    break;
                                case State.Disabled:
                                    _followerClock.Follow(pause.LogicalTime);
                                    _logger.LogDebug(
                                        "Could not pause the heartbeat monitor because the monitor is disabled");
                                    break;
                            }
                            

                            break;
                        case Protocol.Resume resume:
                            switch (state)
                            {
                                case State.Paused paused:
                                {
                                    state = new State.Enabled(
                                        paused.Interval,
                                        paused.Timeout,
                                        wallTime,
                                        wallTime.Add(paused.Timeout),
                                        resume.LogicalTime);
                                    _followerClock.Follow(resume.LogicalTime);
                                    _timer.Change(TimeSpan.Zero, paused.Interval);
                                    break;
                                }
                                case State.Enabled enabled:
                                    state = new State.Enabled(
                                        enabled.Interval,
                                        enabled.Timeout,
                                        wallTime,
                                        wallTime.Add(enabled.Timeout),
                                        resume.LogicalTime);
                                    _followerClock.Follow(resume.LogicalTime);
                                    _timer.Change(TimeSpan.Zero, enabled.Interval);
                                    break;
                                case State.Disabled:
                                    _followerClock.Follow(resume.LogicalTime);
                                    _logger.LogDebug(
                                        "Could not resume the heartbeat monitor because the monitor is disabled");
                                    break;
                            }

                            break;
                        case Protocol.CheckFailed failed:
                            // if AxonServer indicates it doesn't know this instruction, we have at least reached it.
                            // We can assume the connection is alive
                            if (ErrorCategory.Parse(failed.Error.ErrorCode)
                                .Equals(ErrorCategory.UnsupportedInstruction))
                            {
                                switch (state)
                                {
                                    case State.Enabled enabled when failed.LogicalTime == enabled.LogicalTime:
                                        var nextHeartbeatCheckDeadlineAt = DateTimeOffsetMath.Max(
                                            _clock().Add(enabled.Timeout).Add(enabled.Interval),
                                            enabled.NextHeartbeatCheckDeadlineAt);
                                        state = enabled with
                                        {
                                            NextHeartbeatCheckDeadlineAt = nextHeartbeatCheckDeadlineAt
                                        };
                                        _logger.LogDebug(
                                            "Heartbeat acknowledgement received. Extending deadline to {NewDeadLine}",
                                            nextHeartbeatCheckDeadlineAt);
                                        break;
                                    case State.Enabled enabled when failed.LogicalTime != enabled.LogicalTime:
                                        _logger.LogDebug("Heartbeat acknowledgement received but it is outdated");
                                        break;
                                    default:
                                        _logger.LogDebug(
                                            "Heartbeat acknowledgement received when the monitor is disabled or paused");
                                        break;
                                }
                            }
                            else
                            {
                                _logger.LogError(
                                    "Heartbeat acknowledgement received but failed with a server error of {ErrorCode}: {ErrorMessage} {ErrorLocation} {Details}",
                                    failed.Error.ErrorCode,
                                    failed.Error.Message,
                                    failed.Error.Location,
                                    string.Join(Environment.NewLine, failed.Error.Details.Select(line => line)));
                            }

                            break;
                        case Protocol.CheckSucceeded succeeded:
                            switch (state)
                            {
                                case State.Enabled enabled when succeeded.LogicalTime == enabled.LogicalTime:
                                    var nextHeartbeatCheckDeadlineAt = DateTimeOffsetMath.Max(
                                        wallTime.Add(enabled.Timeout).Add(enabled.Interval),
                                        enabled.NextHeartbeatCheckDeadlineAt);
                                    state = enabled with
                                    {
                                        NextHeartbeatCheckDeadlineAt = nextHeartbeatCheckDeadlineAt
                                    };
                                    _logger.LogDebug(
                                        "Heartbeat acknowledgement received. Extending deadline to {NewDeadLine}",
                                        nextHeartbeatCheckDeadlineAt);
                                    break;
                                case State.Enabled enabled when succeeded.LogicalTime != enabled.LogicalTime:
                                    _logger.LogDebug("Heartbeat acknowledgement received but it is outdated");
                                    break;
                                default:
                                    _logger.LogDebug(
                                        "Heartbeat acknowledgement received when the monitor is disabled or paused");
                                    break;
                            }

                            break;
                        case Protocol.ReceiveServerHeartbeat receive:
                            switch (state)
                            {
                                case State.Enabled enabled when receive.LogicalTime == enabled.LogicalTime:
                                    if (enabled.NextHeartbeatCheckAt <= wallTime)
                                    {
                                        enabled = enabled with
                                        {
                                            NextHeartbeatCheckAt = wallTime.Add(enabled.Interval)
                                        };
                                        _logger.LogDebug(
                                            "Extended heartbeat check to {NewCheck}",
                                            enabled.NextHeartbeatCheckAt);
                                    }

                                    enabled = enabled with
                                    {
                                        NextHeartbeatCheckDeadlineAt = DateTimeOffsetMath.Max(
                                            wallTime.Add(enabled.Interval), enabled.NextHeartbeatCheckDeadlineAt)
                                    };

                                    _logger.LogDebug(
                                        "Axon Server Heartbeat received. Extending deadline to {NewDeadLine}",
                                        enabled.NextHeartbeatCheckDeadlineAt);
                                    state = enabled;

                                    break;
                                case State.Enabled enabled when receive.LogicalTime != enabled.LogicalTime:
                                    _logger.LogDebug(
                                        "Axon Server Heartbeat received but it is outdated");
                                    break;
                                default:
                                    _logger.LogDebug(
                                        "Axon Server Heartbeat received when the monitor is disabled or paused");
                                    break;
                            }

                            break;
                    }
                    _logger.LogDebug("Completed {Message} with {State}", message.ToString(), state.ToString());
                }
            }
        }
        catch (TaskCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Heartbeat monitor message loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Heartbeat monitor message loop is exciting because an operation was cancelled");
        }
    }

    private record Protocol(long LogicalTime)
    {
        public record Enable(TimeSpan Interval, TimeSpan Timeout, long LogicalTime) : Protocol(LogicalTime);
        public record Check(DateTimeOffset Instant, long LogicalTime) : Protocol(LogicalTime);
        public record Disable(long LogicalTime) : Protocol(LogicalTime);
        public record Pause(long LogicalTime) : Protocol(LogicalTime);
        public record Resume(long LogicalTime) : Protocol(LogicalTime);
        public record CheckSucceeded(long LogicalTime) : Protocol(LogicalTime);
        public record CheckFailed(ErrorMessage Error, long LogicalTime) : Protocol(LogicalTime);
        public record ReceiveServerHeartbeat(long LogicalTime) : Protocol(LogicalTime);
    }

    private record State(long LogicalTime)
    {
        public record Disabled(long LogicalTime) : State(LogicalTime);
        public record Enabled(TimeSpan Interval, TimeSpan Timeout, DateTimeOffset NextHeartbeatCheckAt, DateTimeOffset NextHeartbeatCheckDeadlineAt, long LogicalTime) : State(LogicalTime);
        public record Paused(TimeSpan Interval, TimeSpan Timeout, long LogicalTime) : State(LogicalTime);
    }

    /// <summary>
    /// Logical clock that increases with each important state change coming from callers.
    /// </summary>
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

    /// <summary>
    /// Logical clock that follows the leader clock as state changes are observed by the channel processing loop  
    /// </summary>
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
                TimeSpanMath.Max(interval, _minimumCheckInterval),
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

    public async Task ReceiveServerHeartbeat()
    {
        await _inbox.Writer.WriteAsync(
            new Protocol.ReceiveServerHeartbeat(
                _leaderClock.LogicalTime
            )
        );
    }
    
    public event EventHandler? HeartbeatMissed;
    
    protected virtual void OnHeartbeatMissed()
    {
        HeartbeatMissed?.Invoke(this, EventArgs.Empty);
    }
    
    public async ValueTask DisposeAsync()
    {
        _inboxCancellation.Cancel();
        _inbox.Writer.Complete();
        await _inbox.Reader.Completion;
        await _protocol;
        await _timer.DisposeAsync();
        _inboxCancellation.Dispose();
        _protocol.Dispose();
    }
}