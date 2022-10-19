using Grpc.Core;
using Io.Axoniq.Axonserver.Grpc.Event;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

public class AppendEventsTransaction : IAppendEventsTransaction
{
    private readonly AsyncClientStreamingCall<Event, Confirmation> _call;
    private readonly ILogger<AppendEventsTransaction> _logger;
    private readonly Guid _transactionId;
    private int _disposed;

    internal AppendEventsTransaction(AsyncClientStreamingCall<Event, Confirmation> call, ILogger<AppendEventsTransaction> logger)
    {
        _call = call ?? throw new ArgumentNullException(nameof(call));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transactionId = Guid.NewGuid();
    }

    public Task AppendEventAsync(Event @event)
    {
        _logger.LogDebug("Appending event with identifier {MessageIdentifier} in transaction {TransactionId}", @event.MessageIdentifier, _transactionId.ToString("N"));
        return _call.RequestStream.WriteAsync(@event);
    }

    public async Task<Confirmation> CommitAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            _logger.LogDebug("Committing transaction {TransactionId}", _transactionId.ToString("N"));
            await _call.RequestStream.CompleteAsync().ConfigureAwait(false);
        }
        return await _call.ResponseAsync.ConfigureAwait(false);
    }

    public Task Rollback()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            _logger.LogDebug("Rolling back transaction {TransactionId}", _transactionId.ToString("N"));
            _call.Dispose();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            _call.Dispose();
        }
    }
}