using System.Collections.Concurrent;
using AxonIQ.AxonServer.Grpc;
using AxonIQ.AxonServer.Grpc.Control;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class HeartbeatClient
{
    private readonly WritePlatformOutboundInstruction _writer;
    private readonly ILogger<HeartbeatClient> _logger;
    private readonly ConcurrentDictionary<string, ReceiveHeartbeatAcknowledgement> _responders;

    public HeartbeatClient(WritePlatformOutboundInstruction writer, ILogger<HeartbeatClient> logger)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _responders = new ConcurrentDictionary<string, ReceiveHeartbeatAcknowledgement>();
    }

    public ValueTask Receive(InstructionAck message)
    {
        return !string.IsNullOrEmpty(message.InstructionId) &&
               _responders.TryRemove(message.InstructionId, out var responder)
            ? responder(message)
            : ValueTask.CompletedTask;
    }
    
    public Task Send(ReceiveHeartbeatAcknowledgement responder)
    {
        var instruction = new PlatformOutboundInstruction
        {
            InstructionId = Guid.NewGuid().ToString("N"),
            Heartbeat = new Heartbeat()
        };
        var result = _writer(instruction);
        if (!_responders.TryAdd(instruction.InstructionId, responder))
        {
            // As long as the instruction id is a Guid, the chance of collision is close to zero.
            _logger.LogWarning("The heartbeat instruction identifier {InstructionId} appears to be taken. Could not register a matching acknowledgement responder",
                instruction.InstructionId);
        }
        return result;
    }
}