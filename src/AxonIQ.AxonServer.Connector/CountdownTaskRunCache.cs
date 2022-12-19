namespace AxonIQ.AxonServer.Connector;

internal class CountdownTaskRunCache
{
    private static long _token;
    
    public static long NextToken() => Interlocked.Increment(ref _token);
    
    private readonly Dictionary<long, Task> _tasks;
    private readonly Dictionary<long, int> _countdowns;
    
    public CountdownTaskRunCache()
    {
        _tasks = new Dictionary<long, Task>();
        _countdowns = new Dictionary<long, int>();
    }

    public void Add(long token, int initialCount, Task task)
    {
        _tasks.Add(token, task);
        _countdowns.Add(token, initialCount);
    }

    public bool Signal(long token, out Task? task)
    {
        if (_countdowns.TryGetValue(token, out var currentCount))
        {
            var nextCount = currentCount - 1; 
            if (nextCount == 0)
            {
                _countdowns.Remove(token);
                return _tasks.Remove(token, out task);
            }
            _countdowns[token] = nextCount;
        }
        task = default;
        return false;
    }

    public bool TryRemove(long token, out Task? task)
    {
        _countdowns.Remove(token);
        return _tasks.Remove(token, out task);
    }
    
    public (long, Task)[] Clear()
    {
        var tasks = _tasks.Select(pair => (pair.Key, pair.Value)).ToArray();
        _tasks.Clear();
        return tasks;
    }
}