namespace AxonIQ.AxonServer.Connector;

internal class TaskCache
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<ulong, (DateTimeOffset, Task)> _tasks;

    public TaskCache(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _tasks = new Dictionary<ulong, (DateTimeOffset, Task)>();
    }
    
    public void Add(ulong token, Task task)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        _tasks.Add(token, (_clock(), task));
    }

    public bool TryRemove(ulong token, out Task task)
    {
        var removed = _tasks.Remove(token, out var value);
        if (removed)
        {
            task = value.Item2;
            return true;
        }

        task = Task.CompletedTask;
        return false;
    }

    public Task[] Purge(TimeSpan threshold)
    {
        var absoluteThreshold = _clock().Subtract(threshold);
        var overdue = _tasks
            .Where(entry => entry.Value.Item1 < absoluteThreshold)
            .ToArray();
        foreach (var entry in overdue)
        {
            _tasks.Remove(entry.Key);
        }

        return Array.ConvertAll(overdue, entry => entry.Value.Item2);
    }
    
    public static class Token
    {
        private static ulong _token;

        public static ulong Next()
        {
            var token = _token;
            unchecked // allows the token to overflow should we ever hit that
            {
                _token += 1; 
            }
            return token;
        }
    }
}