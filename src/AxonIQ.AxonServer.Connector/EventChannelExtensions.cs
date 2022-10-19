using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public static class EventChannelExtensions
{
    public static async Task<Confirmation> AppendEvents(this IEventChannel channel, params Event[] events)
    {
        var transaction = channel.StartAppendEventsTransaction();
        foreach (var @event in events) await transaction.AppendEventAsync(@event).ConfigureAwait(false);
        return await transaction.CommitAsync().ConfigureAwait(false);
    }
}