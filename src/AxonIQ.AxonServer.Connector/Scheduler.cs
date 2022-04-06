using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class Scheduler : IScheduler
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _timerFrequency;
    private readonly ILogger<Scheduler> _logger;
    
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    private readonly Timer _timer;

    public Scheduler(Func<DateTimeOffset> clock, TimeSpan timerFrequency, ILogger<Scheduler> logger)
    {
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (logger == null) throw new ArgumentNullException(nameof(logger));

        _clock = clock;
        _timerFrequency = timerFrequency;
        _logger = logger;
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
        _timer = new Timer(_ =>
        {
            var message = new Protocol.Tick(clock());
            if (!_inbox.Writer.TryWrite(message))
            {
                _logger.LogDebug("Could not tell the scheduler to tick because the inbox refused to accept the message");
            }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public Func<DateTimeOffset> Clock => _clock;

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        var all = new HashSet<ScheduledTask>();
        while (await _inbox.Reader.WaitToReadAsync(ct))
        {
            while (_inbox.Reader.TryRead(out var message))
            {
                switch (message)
                {
                    case Protocol.Tick tick:
                        
                        var due = all.Where(scheduled => scheduled.Due <= tick.Time).ToArray();
                        _logger.LogDebug("Scheduler has {Count} tasks due", due.Length);
                        foreach (var scheduled in due)
                        {
                            await scheduled.Task();
                        }
                        all.ExceptWith(due);
                        if (all.Count == 0)
                        {
                            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                            _logger.LogDebug("Scheduler timer was disabled because there are no tasks");
                        }
                        break;
                    case Protocol.ScheduleTask schedule:
                        if (schedule.Due < _clock())
                        {
                            await schedule.Task();
                        }
                        else
                        {
                            if (all.Count == 0)
                            {
                                _timer.Change(_timerFrequency, _timerFrequency);
                                _logger.LogDebug("Scheduler timer was enabled because there are tasks");
                            }

                            all.Add(new ScheduledTask(schedule.Task, schedule.Due));
                        }

                        break;
                }
            }
        }
    }

    private record ScheduledTask(Func<ValueTask> Task, DateTimeOffset Due);
    
    private record Protocol
    {
        public record Tick(DateTimeOffset Time) : Protocol;

        public record ScheduleTask(Func<ValueTask> Task, DateTimeOffset Due) : Protocol;
    }

    public async ValueTask ScheduleTask(Func<ValueTask> task, DateTimeOffset due)
    {
        await _inbox.Writer.WriteAsync(new Protocol.ScheduleTask(task, due));
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