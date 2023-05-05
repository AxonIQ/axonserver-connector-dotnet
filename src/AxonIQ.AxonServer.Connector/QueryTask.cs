namespace AxonIQ.AxonServer.Connector;

internal class QueryTask : IDisposable
{
    private readonly Task _task;

    public QueryTask(Task task)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void Dispose() => _task.Dispose();
}