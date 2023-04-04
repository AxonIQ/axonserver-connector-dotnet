namespace AxonIQ.AxonServer.Connector;

public interface IScheduler : IAsyncDisposable
{
    Func<DateTimeOffset> Clock { get; }
    
    ValueTask ScheduleTask(Func<ValueTask> task, DateTimeOffset due);
    
    ValueTask ScheduleTask(Func<ValueTask> task, TimeSpan due);
}