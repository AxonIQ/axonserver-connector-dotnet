namespace AxonIQ.AxonServer.Connector;

public class CountdownCompletionSource
{
    private readonly TaskCompletionSource _source;
    private readonly List<Exception> _exceptions;
    private readonly Task _completion;

    public CountdownCompletionSource(int initialCount)
    {
        if (initialCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCount), initialCount,
                "The initial count can not be less than or equal to 0");

        InitialCount = initialCount;
        CurrentCount = 0;
        _exceptions = new List<Exception>();
        _source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = InitialCount != 0 ? _source.Task : Task.CompletedTask;
    }

    public int InitialCount { get; }
    public int CurrentCount { get; private set; }

    public Task Completion => _completion;

    public bool TrySignalFailure(Exception exception)
    {
        if (_source.Task.IsCompleted) return false;

        _exceptions.Add(exception);
        CurrentCount++;

        return CurrentCount == InitialCount && _source.TrySetException(_exceptions);
    }

    public bool TrySignalSuccess()
    {
        if (_source.Task.IsCompleted) return false;

        CurrentCount++;

        return CurrentCount == InitialCount &&
               (_exceptions.Count > 0
                   ? _source.TrySetException(_exceptions)
                   : _source.TrySetResult());
    }

    public bool Fault(Exception exception)
    {
        if (_source.Task.IsCompleted) return false;
        
        return _source.TrySetException(exception);
    }
}
