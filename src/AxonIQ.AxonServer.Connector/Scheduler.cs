using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal class Scheduler : IScheduler
{
    public static readonly TimeSpan DefaultTickFrequency = TimeSpan.FromMilliseconds(10);
    
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _frequency;
    private readonly ILogger<Scheduler> _logger;
    
    private readonly Channel<Message> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _consumer;
    private readonly Timer _timer;

    private long _disposed;

    public Scheduler(Func<DateTimeOffset> clock, TimeSpan frequency, ILogger<Scheduler> logger)
    {
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        if (frequency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency,
                "The frequency with which the scheduler checks for due scheduled tasks can not be negative.");
        }

        _clock = clock;
        _frequency = frequency;
        _logger = logger;
        _inboxCancellation = new CancellationTokenSource();
        _inbox = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _timer = new Timer(_ =>
        {
            var message = new Message.Tick(clock());
            if (!_inbox.Writer.TryWrite(message))
            {
                _logger.LogDebug("Could not tell the scheduler to tick because the inbox refused to accept the message");
            }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _consumer = ConsumeInbox(_inboxCancellation.Token);
    }

    public Func<DateTimeOffset> Clock => _clock;

    private async Task ConsumeInbox(CancellationToken ct)
    {
        var all = new List<ScheduledTask>();
        try
        {
            while (await _inbox.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_inbox.Reader.TryRead(out var message))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Began {Message}", message.ToString());
                    }
                    switch (message)
                    {
                        case Message.Tick tick:
                            var due = all.Where(scheduled => scheduled.Due <= tick.Time).ToArray();
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Scheduler has {Count} tasks due", due.Length);
                            }

                            foreach (var scheduled in due)
                            {
                                try
                                {
                                    await scheduled.Task().ConfigureAwait(false);
                                }
                                catch(OperationCanceledException exception)
                                {
                                    _logger.LogDebug(exception,
                                        "Scheduled task could not be executed because an operation was cancelled");
                                }
                                catch (Exception exception)
                                {
                                    _logger.LogCritical(exception, "Scheduled task could not be executed due to an unexpected exception");
                                }

                                all.Remove(scheduled);
                            }
                            
                            if (all.Count == 0)
                            {
                                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                if (_logger.IsEnabled(LogLevel.Debug))
                                {
                                    _logger.LogDebug("Scheduler timer was disabled because there are no tasks");
                                }
                            }
                            break;
                        case Message.ScheduleTask schedule:
                            if (schedule.Due < _clock())
                            {
                                try
                                {
                                    await schedule.Task().ConfigureAwait(false);
                                }
                                catch(OperationCanceledException exception)
                                {
                                    _logger.LogDebug(exception,
                                    "Scheduled task could not be executed because an operation was cancelled");
                                }
                                catch (Exception exception)
                                {
                                    _logger.LogCritical(exception, "Scheduled task could not be executed due to an unexpected exception");
                                }
                            }
                            else
                            {
                                if (all.Count == 0)
                                {
                                    _timer.Change(_frequency, _frequency);
                                    if (_logger.IsEnabled(LogLevel.Debug))
                                    {
                                        _logger.LogDebug("Scheduler timer was enabled because there are tasks");
                                    }
                                }

                                all.Add(new ScheduledTask(schedule.Task, schedule.Due));
                            }

                            break;
                    }
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Completed {Message}", message.ToString());
                    }
                }
            }
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Scheduler protocol loop is exciting because an operation was cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Scheduler protocol loop is exciting because of an unexpected exception");
        }
    }

    private record ScheduledTask(Func<ValueTask> Task, DateTimeOffset Due);
    
    private record Message
    {
        public record Tick(DateTimeOffset Time) : Message;

        public record ScheduleTask(Func<ValueTask> Task, DateTimeOffset Due) : Message;
    }
    
    public ValueTask ScheduleTaskAsync(Func<ValueTask> task, TimeSpan due)
    {
        ThrowIfDisposed();
        return _inbox.Writer.WriteAsync(new Message.ScheduleTask(task, _clock().Add(due)));
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.Read(ref _disposed) == Disposed.Yes) 
            throw new ObjectDisposedException(nameof(Scheduler));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            _inboxCancellation.Cancel();
            _inbox.Writer.Complete();
            await _consumer.ConfigureAwait(false);
            await _timer.DisposeAsync().ConfigureAwait(false);
            _inboxCancellation.Dispose();
            _consumer.Dispose();
        }
    }
}