using System.Collections.Concurrent;
using PooledAwait;

namespace AxonIQ.AxonServer.Connector;

internal class ConcurrentFlowControl : IFlowControl
{
    private readonly ConcurrentBag<PooledValueTaskSource<bool>> _waiters = new();
    private long _requested = 0L;
    private long _cancelled = Cancelled.No;

    public void Request(long count)
    {
        if (Interlocked.Read(ref _cancelled) == Cancelled.No && Interlocked.Add(ref _requested, count) > 0)
        {
            SignalWaiters(true);
        }
    }

    public bool TryTake()
    {
        if (Interlocked.Read(ref _cancelled) == Cancelled.Yes)
        {
            return false;
        }
        
        // Try take a permit and return true if we succeeded
        if (Interlocked.Decrement(ref _requested) >= 0)
        {
            return true;
        }

        // Return the permit because there was none to be taken (we're not allowed to have negative permits)
        // If we ended up with a positive number of permits again, we signal any waiters it's okay to try take one
        if (Interlocked.Increment(ref _requested) > 0)
        {
            SignalWaiters(true);
        }
        return false;
    }

    public ValueTask<bool> WaitToTakeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Read(ref _cancelled) == Cancelled.Yes)
        {
            return ValueTask.FromResult(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(cancellationToken);
        }
        
        if (Interlocked.Read(ref _requested) > 0)
        {
            return ValueTask.FromResult(true);
        }

        var waiter = PooledValueTaskSource<bool>.Create();
        _waiters.Add(waiter);
        
        // REMARK: We may have been racing with .Cancel()
        // We need to signal at least this waiter when that happens.
        // Because we've already added the waiter we know it will be signalled.
        if (Interlocked.Read(ref _cancelled) == Cancelled.Yes)
        {
            SignalWaiters(false);
        }
        
        return waiter.Task;
    }
    
    public void Cancel()
    {
        if (Interlocked.CompareExchange(ref _cancelled, Cancelled.Yes, Cancelled.No) == Cancelled.No)
        {
            SignalWaiters(false);
        }
    }

    private void SignalWaiters(bool result)
    {
        // We're only removing as many waiters as we know of at a given point in time.
        // Otherwise this method never finishes
        var count = _waiters.Count;
        while (count > 0 && _waiters.TryTake(out var waiter))
        {
            waiter.TrySetResult(result);
            count--;
        }
    }
}