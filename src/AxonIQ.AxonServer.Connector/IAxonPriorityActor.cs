namespace AxonIQ.AxonServer.Connector;

internal interface IAxonPriorityActor<TMessage> : IAxonActor<TMessage>
{
    ValueTask ScheduleAsync(MessagePriority priority, TMessage message, TimeSpan due);
    ValueTask ScheduleAsync(MessagePriority priority, TMessage message, TimeSpan due, CancellationToken ct);
    
    ValueTask TellAsync(MessagePriority priority, TMessage message);
    ValueTask TellAsync(MessagePriority priority, TMessage message, CancellationToken ct);
}