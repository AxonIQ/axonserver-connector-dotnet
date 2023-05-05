namespace AxonIQ.AxonServer.Connector;

internal class CommandHandlerRegistration : ICommandHandlerRegistration
{
    private long _disposed;
    
    private readonly Func<Task> _unsubscribe;
    private readonly Task _subscribeCompletion;

    internal CommandHandlerRegistration(Task subscribeCompletion, Func<Task> unsubscribe)
    {
        _subscribeCompletion = subscribeCompletion ?? throw new ArgumentNullException(nameof(subscribeCompletion));;
        _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }

    public Task WaitUntilCompletedAsync() => _subscribeCompletion;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            await _unsubscribe().ConfigureAwait(false);
        }    
    }
}