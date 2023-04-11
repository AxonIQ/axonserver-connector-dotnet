namespace AxonIQ.AxonServer.Connector;

public interface IScheduler : IAsyncDisposable
{
    Func<DateTimeOffset> Clock { get; }
    
    ValueTask ScheduleTaskAsync(Func<ValueTask> task, DateTimeOffset due);
    
    ValueTask ScheduleTaskAsync(Func<ValueTask> task, TimeSpan due);
}