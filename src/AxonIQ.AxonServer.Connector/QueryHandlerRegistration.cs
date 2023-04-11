namespace AxonIQ.AxonServer.Connector;

public class QueryHandlerRegistration : IQueryHandlerRegistration
{
    private int _disposed;
    
    private readonly Func<Task> _unsubscribe;
    private readonly Task _subscribeCompletion;

    internal QueryHandlerRegistration(Task subscribeCompletion, Func<Task> unsubscribe)
    {
        _subscribeCompletion = subscribeCompletion ?? throw new ArgumentNullException(nameof(subscribeCompletion));;
        _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }

    public Task WaitUntilCompletedAsync() => _subscribeCompletion;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            await _unsubscribe().ConfigureAwait(false);
        }    
    }
}