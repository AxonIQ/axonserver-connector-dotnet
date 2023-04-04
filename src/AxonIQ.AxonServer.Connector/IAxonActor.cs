namespace AxonIQ.AxonServer.Connector;

internal interface IAxonActor<TMessage>
{
    ValueTask ScheduleAsync(TMessage message, TimeSpan due);
    ValueTask ScheduleAsync(TMessage message, TimeSpan due, CancellationToken ct);

    ValueTask TellAsync(TMessage message);
    ValueTask TellAsync(TMessage message, CancellationToken ct);   
}