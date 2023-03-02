using System.Collections.Immutable;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class HeartbeatChannel : IAsyncDisposable
{
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromSeconds(1.0);
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(15);
    private static readonly Heartbeat HeartbeatInstance = new ();

    private readonly AxonActor<Protocol, State> _actor;
    private readonly WritePlatformInboundInstruction _writer;
    private readonly OnHeartbeatMissed _onHeartbeatMissed;
    private readonly TimeSpan _minimumCheckInterval;
    private readonly TimeSpan _purgeInterval;
    private readonly ILogger<HeartbeatChannel> _logger;
    private readonly VersionClock _versionClock;

    public HeartbeatChannel(WritePlatformInboundInstruction writer, OnHeartbeatMissed onHeartbeatMissed, IScheduler scheduler, ILogger<HeartbeatChannel> logger) 
        : this(writer, onHeartbeatMissed, MinimumCheckInterval, PurgeInterval, scheduler, logger)
    {
    }

    public HeartbeatChannel(WritePlatformInboundInstruction writer, OnHeartbeatMissed onHeartbeatMissed, TimeSpan minimumCheckInterval, TimeSpan purgeInterval, IScheduler scheduler, ILogger<HeartbeatChannel> logger) 
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        if (onHeartbeatMissed == null) throw new ArgumentNullException(nameof(onHeartbeatMissed));
        if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (minimumCheckInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumCheckInterval), minimumCheckInterval,
                "The minimum check interval must be positive");
        }
        if (purgeInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(purgeInterval), purgeInterval,
                "The purge interval must be positive");
        }

        _writer = writer;
        _onHeartbeatMissed = onHeartbeatMissed; 
        _logger = logger;
        _minimumCheckInterval = minimumCheckInterval;
        _purgeInterval = purgeInterval;
        _versionClock = new VersionClock();
        _actor = new AxonActor<Protocol, State>(
            Receive,
            new State.Disabled(ImmutableList<SentHeartbeat>.Empty, _versionClock.LogicalTime),
            scheduler,
            logger
        );
    }

    private async Task<State> Receive(Protocol message, State state, CancellationToken ct)
    {
        var wallTime = _actor.Clock();
        switch (message)
        {
            case Protocol.Enable enable:
                // Note: Enable always causes a state transition regardless of the state we are in 
                state = new State.Enabled(
                    enable.Interval,
                    enable.Timeout,
                    wallTime,
                    wallTime.Add(enable.Timeout),
                    state.SentHeartbeats,
                    enable.LogicalTime);
                await _actor.TellAsync(new Protocol.Check(state.LogicalTime), ct);
                
                break;
            case Protocol.Disable disable:
                state = new State.Disabled(state.SentHeartbeats, disable.LogicalTime);
                break;
            case Protocol.Pause pause:
                switch (state)
                {
                    case State.Enabled enabled:
                    {
                        state = new State.Paused(enabled.Interval, enabled.Timeout, state.SentHeartbeats, pause.LogicalTime);
                        break;
                    }
                    case State.Paused:
                        _logger.LogDebug(
                            "Could not pause the heartbeat monitor because the monitor is already paused");
                        break;
                    case State.Disabled:
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
                            state.SentHeartbeats,
                            resume.LogicalTime);
                        await _actor.TellAsync(new Protocol.Check(state.LogicalTime), ct);
                        break;
                    }
                    case State.Enabled enabled:
                        state = new State.Enabled(
                            enabled.Interval,
                            enabled.Timeout,
                            wallTime,
                            wallTime.Add(enabled.Timeout),
                            state.SentHeartbeats,
                            resume.LogicalTime);
                        await _actor.TellAsync(new Protocol.Check(state.LogicalTime), ct);
                        break;
                    case State.Disabled:
                        _logger.LogDebug(
                            "Could not resume the heartbeat monitor because the monitor is disabled");
                        break;
                }

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
                            await _onHeartbeatMissed();
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
                            var time = enabled.LogicalTime;
                            await _actor.TellAsync(new Protocol.SendClientHeartbeat(time), ct);
                            enabled = enabled with
                            {
                                NextHeartbeatCheckAt = wallTime.Add(enabled.Interval)
                            };

                            _logger.LogDebug(
                                "Extended heartbeat check to {NewCheck}",
                                enabled.NextHeartbeatCheckAt);
                        }

                        state = enabled;
                        
                        await _actor.ScheduleAsync(new Protocol.Check(enabled.LogicalTime), enabled.Interval, ct);

                        break;
                    }
                    case State.Enabled enabled when check.LogicalTime != enabled.LogicalTime:
                        await _actor.ScheduleAsync(new Protocol.Check(enabled.LogicalTime), enabled.Interval, ct);
                        
                        _logger.LogDebug("Could not check heartbeat because the check request is outdated");
                        break;
                    default:
                        _logger.LogDebug("Could not check heartbeat because the monitor is not enabled");
                        break;
                }

                break;
            case Protocol.SendClientHeartbeat send:
                switch (state)
                {
                    // Note: Only send when enabled and the send request is using the latest configuration 
                    case State.Enabled enabled when send.LogicalTime == enabled.LogicalTime:
                    {
                        var id = InstructionId.New();
                        if (state.SentHeartbeats.Count == 0)
                        {
                            await _actor.ScheduleAsync(
                                    new Protocol.PurgeClientHeartbeats(_actor.Clock(), enabled.LogicalTime),
                                    _purgeInterval,
                                    ct)
                                .ConfigureAwait(false);
                        }
                        state = enabled with
                        {
                            SentHeartbeats =
                                enabled.SentHeartbeats.Add(new SentHeartbeat(id, _actor.Clock().Add(enabled.Timeout)))
                        };
                        await _writer(new PlatformInboundInstruction
                        {
                            InstructionId = id.ToString(),
                            Heartbeat = HeartbeatInstance
                        }).ConfigureAwait(false);
                        
                        break;
                    }
                }

                break;
            case Protocol.ReceiveClientHeartbeatAcknowledgement receive:
                var match =
                    state
                        .SentHeartbeats
                        .SingleOrDefault(candidate =>
                            candidate.InstructionId.Equals(new InstructionId(receive.Acknowledgement.InstructionId)));
                if (match != null)
                {
                    if (receive.Acknowledgement.Success)
                    {
                        await _actor.TellAsync(new Protocol.CheckSucceeded(state.LogicalTime), ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _actor.TellAsync(new Protocol.CheckFailed(receive.Acknowledgement.Error, state.LogicalTime), ct).ConfigureAwait(false);
                    }

                    state = state with
                    {
                        SentHeartbeats = state.SentHeartbeats.Remove(match)
                    };
                }
                
                break;
            case Protocol.PurgeClientHeartbeats purge:
                var overdue = 
                    state
                        .SentHeartbeats
                        .Where(candidate => candidate.Due < purge.Due)
                        .ToArray();
                if (overdue.Length != 0)
                {
                    _logger.LogWarning(
                        "Heartbeat channel purge removed {Overdue} client heartbeats that remained unacknowledged",
                        overdue.Length);

                    state = state with
                    {
                        SentHeartbeats = state.SentHeartbeats.RemoveRange(overdue)
                    };
                }

                // Continue purging if more outstanding heartbeats are present
                if (state.SentHeartbeats.Count != 0)
                {
                    await _actor.ScheduleAsync(
                            new Protocol.PurgeClientHeartbeats(_actor.Clock(), state.LogicalTime),
                            _purgeInterval,
                            ct)
                        .ConfigureAwait(false);
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
                                _actor.Clock().Add(enabled.Timeout).Add(enabled.Interval),
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

        return state;
    }
    
    private record Protocol(ulong LogicalTime)
    {
        public record Enable(TimeSpan Interval, TimeSpan Timeout, ulong LogicalTime) : Protocol(LogicalTime);
        public record Check(ulong LogicalTime) : Protocol(LogicalTime);
        public record SendClientHeartbeat(ulong LogicalTime) : Protocol(LogicalTime);
        public record ReceiveClientHeartbeatAcknowledgement(InstructionAck Acknowledgement, ulong LogicalTime) : Protocol(LogicalTime);
        public record PurgeClientHeartbeats(DateTimeOffset Due, ulong LogicalTime) : Protocol(LogicalTime);
        public record Disable(ulong LogicalTime) : Protocol(LogicalTime);
        public record Pause(ulong LogicalTime) : Protocol(LogicalTime);
        public record Resume(ulong LogicalTime) : Protocol(LogicalTime);
        public record CheckSucceeded(ulong LogicalTime) : Protocol(LogicalTime);
        public record CheckFailed(ErrorMessage Error, ulong LogicalTime) : Protocol(LogicalTime);
        public record ReceiveServerHeartbeat(ulong LogicalTime) : Protocol(LogicalTime);
    }
    
    private record SentHeartbeat(InstructionId InstructionId, DateTimeOffset Due);

    private record State(ImmutableList<SentHeartbeat> SentHeartbeats, ulong LogicalTime)
    {
        public record Disabled(ImmutableList<SentHeartbeat> SentHeartbeats, ulong LogicalTime) : State(SentHeartbeats, LogicalTime);
        public record Enabled(TimeSpan Interval, TimeSpan Timeout, DateTimeOffset NextHeartbeatCheckAt, DateTimeOffset NextHeartbeatCheckDeadlineAt, ImmutableList<SentHeartbeat> SentHeartbeats, ulong LogicalTime) : State(SentHeartbeats, LogicalTime);
        public record Paused(TimeSpan Interval, TimeSpan Timeout, ImmutableList<SentHeartbeat> SentHeartbeats, ulong LogicalTime) : State(SentHeartbeats, LogicalTime);
    }

    /// <summary>
    /// Logical clock that increases with each important state change coming from callers.
    /// </summary>
    private class VersionClock
    {
        private ulong _logicalTime;

        public VersionClock()
        {
            _logicalTime = 0UL;
        }

        public ulong Next()
        {
            return Interlocked.Increment(ref _logicalTime);
        }

        public ulong LogicalTime => Interlocked.Read(ref _logicalTime); 
    }
    
    public async Task Enable(TimeSpan interval, TimeSpan timeout)
    {
        await _actor.TellAsync(
            new Protocol.Enable(
                TimeSpanMath.Max(interval, _minimumCheckInterval),
                timeout,
                _versionClock.Next()
            )
        ).ConfigureAwait(false);
    }
    
    public async Task Disable()
    {
        await _actor.TellAsync(
            new Protocol.Disable(
                _versionClock.Next()
            )
        ).ConfigureAwait(false);
    }
    
    public async Task Pause()
    {
        await _actor.TellAsync(
            new Protocol.Pause(
                _versionClock.Next()
            )
        ).ConfigureAwait(false);
    }
    
    public async Task Resume()
    {
        await _actor.TellAsync(
            new Protocol.Resume(
                _versionClock.Next()
            )
        ).ConfigureAwait(false);
    }

    public async Task ReceiveServerHeartbeat()
    {
        await _actor.TellAsync(
            new Protocol.ReceiveServerHeartbeat(
                _versionClock.LogicalTime
            )
        ).ConfigureAwait(false);
    }
    
    public async Task ReceiveClientHeartbeatAcknowledgement(InstructionAck acknowledgement)
    {
        await _actor.TellAsync(
            new Protocol.ReceiveClientHeartbeatAcknowledgement(
                acknowledgement,
                _versionClock.LogicalTime
            )
        ).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _actor.DisposeAsync();
}