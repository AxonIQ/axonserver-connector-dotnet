using Microsoft.VisualStudio.Threading;

namespace AxonIQ.AxonServer.Connector;

internal class ConcurrentFlowControl : IFlowControl
{
    private readonly AsyncManualResetEvent _event = new(false);
    private long _requested;
    private long _cancelled = Cancelled.No;

    public Task<bool> WaitToTakeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Read(ref _cancelled) == Cancelled.Yes)
        {
            return Task.FromResult(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }
        
        if (Interlocked.Read(ref _requested) > 0)
        {
            return Task.FromResult(true);
        }

        return WaitToTakeCoreAsync(cancellationToken);

        async Task<bool> WaitToTakeCoreAsync(CancellationToken ct)
        {
            await _event.WaitAsync(ct);
            return Interlocked.Read(ref _cancelled) == Cancelled.No;
        }
    }
    
    public bool TryTake()
    {
        if (Interlocked.Read(ref _cancelled) == Cancelled.Yes)
        {
            return false;
        }
        
        // Try take an available permit
        var decremented = Interlocked.Decrement(ref _requested);
        if (decremented >= 0)
        {
            if (decremented == 0)
            {
                _event.Reset();
            }
            return true;
        }

        // Return the unavailable permit (if the result is negative it means there are no permits available)
        if (Interlocked.Increment(ref _requested) > 0 && Interlocked.Read(ref _cancelled) == Cancelled.No)
        {
            // If we ended up with a positive number of permits after returning the one we tried to take,
            // we signal any waiters it's okay to try take one
            _event.Set();
        }
        return false;
    }
    
    public void Request(long count)
    {
        if (count > 0 && 
            Interlocked.Add(ref _requested, count) > 0 && 
            Interlocked.Read(ref _cancelled) == Cancelled.No)
        {
            // Signal any waiters that they should try to take a permit
            _event.Set();
        }
    }

    public void Cancel()
    {
        if (Interlocked.CompareExchange(ref _cancelled, Cancelled.Yes, Cancelled.No) == Cancelled.No)
        {
            // Signal any waiters that they should stop waiting and not try to take a permit (cancelled)
            _event.Set();
        }
    }
}