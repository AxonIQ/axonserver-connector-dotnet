namespace AxonIQ.AxonServer.Connector;

internal class EventProcessorRegistration : IEventProcessorRegistration
{
    private int _disposed;
    
    private readonly Func<Task> _unsubscribe;
    private readonly Task _subscribeCompletion;

    internal EventProcessorRegistration(Task subscribeCompletion, Func<Task> unsubscribe)
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