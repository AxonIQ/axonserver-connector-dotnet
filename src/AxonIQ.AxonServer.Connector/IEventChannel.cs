using Google.Protobuf.WellKnownTypes;
using Io.Axoniq.Axonserver.Grpc;
using Io.Axoniq.Axonserver.Grpc.Event;

namespace AxonIQ.AxonServer.Connector;

public interface IEventChannel
{
    IAppendEventsTransaction StartAppendEventsTransaction();
    
    // Scheduling
    Task<ScheduledEventCancellationToken> ScheduleEventAsync(Duration duration, Event @event);

    Task<ScheduledEventCancellationToken> ScheduleEventAsync(DateTimeOffset instant, Event @event);

    Task<InstructionAck> CancelScheduleAsync(ScheduledEventCancellationToken token);

    Task<ScheduledEventCancellationToken> RescheduleAsync(ScheduledEventCancellationToken token, Duration duration,
        Event @event);
    
    Task<ScheduledEventCancellationToken> RescheduleAsync(ScheduledEventCancellationToken token, DateTimeOffset instant,
        Event @event);

    Task<EventSequenceNumber> FindHighestSequenceAsync(AggregateId id);

    Task<IEventStream> OpenStreamAsync(EventStreamToken token, PermitCount bufferSize, PermitCount? refillBatch = default, bool forceReadFromLeader = false);

    IAggregateEventStream OpenStream(AggregateId id, bool allowSnapshots = true);
    IAggregateEventStream OpenStream(AggregateId id, EventSequenceNumber from, EventSequenceNumber? to = default);

    Task<Confirmation> AppendSnapshotAsync(Event snapshot);

    IAggregateEventStream LoadSnapshots(AggregateId id, EventSequenceNumber? from = default, EventSequenceNumber? to = default,
        int maxResults = 1);

    Task<EventStreamToken> GetLastTokenAsync();
    Task<EventStreamToken> GetFirstTokenAsync();
    Task<EventStreamToken> GetTokenAtAsync(long instant);
    
    IAsyncEnumerable<IEventQueryResultEntry> QueryEvents(string expression, bool liveStream);
    IAsyncEnumerable<IEventQueryResultEntry> QuerySnapshotEvents(string expression, bool liveStream);
}