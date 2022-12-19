namespace AxonIQ.AxonServer.Connector;

internal class TaskRunCache
{
    private long _token;
    private readonly Dictionary<long, Task> _tasks;
    
    public TaskRunCache()
    {
        _tasks = new Dictionary<long, Task>();
    }

    public long NextToken()
    {
        var token = _token;
        _token += 1;
        return token;
    }
    
    public void Add(long token, Task task) => _tasks.Add(token, task);
    public bool TryRemove(long token, out Task? task) => _tasks.Remove(token, out task);

    public (long, Task)[] Clear()
    {
        var tasks = _tasks.Select(pair => (pair.Key, pair.Value)).ToArray();
        _tasks.Clear();
        return tasks;
    }
}