using System.Threading.Channels;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Control;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class HeartbeatChannel : IAsyncDisposable
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(15);
    private static readonly Heartbeat HeartbeatInstance = new ();
    
    private readonly Func<DateTimeOffset> _clock;
    private readonly WritePlatformInboundInstruction _writer;
    private readonly TimeSpan _purgeInterval;
    private readonly ILogger<HeartbeatChannel> _logger;
    
    private readonly Channel<Protocol> _inbox;
    private readonly CancellationTokenSource _inboxCancellation;
    private readonly Task _protocol;
    private readonly Timer _timer;

    public HeartbeatChannel(
        Func<DateTimeOffset> clock, 
        WritePlatformInboundInstruction writer,
        ILogger<HeartbeatChannel> logger)
    : this(clock, writer, PurgeInterval, logger)
    {
    }

    internal HeartbeatChannel(
        Func<DateTimeOffset> clock, 
        WritePlatformInboundInstruction writer,
        TimeSpan purgeInterval,
        ILogger<HeartbeatChannel> logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _purgeInterval = purgeInterval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inbox = Channel.CreateUnbounded<Protocol>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inboxCancellation = new CancellationTokenSource();
        _timer = new Timer(_ =>
        {
            var message = new Protocol.Purge(clock());
            if (!_inbox.Writer.TryWrite(message))
            {
                _logger.LogDebug("Could not tell the heartbeat channel to purge because its inbox refused to accept the message");
            }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _protocol = RunChannelProtocol(_inboxCancellation.Token);
    }

    private async Task RunChannelProtocol(CancellationToken ct)
    {
        var all = new HashSet<SentHeartbeat>();
        try
        {
            while (await _inbox.Reader.WaitToReadAsync(ct))
            {
                while (_inbox.Reader.TryRead(out var message))
                {
                    _logger.LogDebug("Began {Message}", message.ToString());
                    switch (message)
                    {
                        case Protocol.Send send:
                            var id = Guid.NewGuid().ToString("D");
                            if (all.Count == 0)
                            {
                                if (!_timer.Change(_purgeInterval, _purgeInterval))
                                {
                                    _logger.LogWarning(
                                        "Heartbeat channel timer could not be enabled because the timer refused to do so");
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Heartbeat channel timer was enabled because there are heartbeats to monitor");
                                }
                            }

                            all.Add(new SentHeartbeat(id, send.Responder, _clock().Add(send.Timeout)));
                            await _writer(new PlatformInboundInstruction
                            {
                                InstructionId = id,
                                Heartbeat = HeartbeatInstance
                            });
                            break;
                        case Protocol.Purge purge:
                            var overdue = all.RemoveWhere(candidate => candidate.Due < purge.Due);
                            if (overdue != 0)
                            {
                                _logger.LogWarning(
                                    "Heartbeat channel purge removed {Overdue} heartbeats that remained unacknowledged",
                                    overdue);
                            }

                            if (all.Count == 0)
                            {
                                if (!_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan))
                                {
                                    _logger.LogWarning(
                                        "Heartbeat channel timer could not be disabled because the timer refused to do so");
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Heartbeat channel timer was disabled because there are no more heartbeats to monitor");
                                }
                            }

                            break;
                        case Protocol.Receive receive:
                            var heartbeats =
                                all
                                    .Where(candidate => candidate.InstructionId == receive.Message.InstructionId)
                                    .ToArray();
                            foreach (var heartbeat in heartbeats)
                            {
                                await heartbeat.Responder(receive.Message);
                            }

                            all.ExceptWith(heartbeats);
                            if (all.Count == 0)
                            {
                                if (!_timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan))
                                {
                                    _logger.LogWarning(
                                        "Heartbeat channel timer could not be disabled because the timer refused to do so");
                                }
                                else
                                {
                                    _logger.LogDebug(
                                        "Heartbeat channel timer was disabled because there are no more heartbeats to monitor");
                                }
                            }

                            break;
                    }
                    _logger.LogDebug("Completed {Message}", message.ToString());
                }
            }
        }
        catch (TaskCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Heartbeat channel message loop is exciting because a task was cancelled");
        }
        catch (OperationCanceledException exception)
        { 
            _logger.LogDebug(exception,
                "Heartbeat channel message loop is exciting because an operation was cancelled");
        }
    }

    private record SentHeartbeat(string InstructionId, ReceiveHeartbeatAcknowledgement Responder, DateTimeOffset Due);
    
    private record Protocol
    {
        public record Purge(DateTimeOffset Due) : Protocol;
        public record Send(ReceiveHeartbeatAcknowledgement Responder, TimeSpan Timeout) : Protocol;
        public record Receive(InstructionAck Message) : Protocol;
    }

    public ValueTask Receive(InstructionAck message)
    {
        return _inbox.Writer.WriteAsync(new Protocol.Receive(message));
    }
    
    public ValueTask Send(ReceiveHeartbeatAcknowledgement responder, TimeSpan timeout)
    {
        return _inbox.Writer.WriteAsync(new Protocol.Send(responder, timeout));
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