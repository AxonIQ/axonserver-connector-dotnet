using System.Collections.Concurrent;
using PooledAwait;

namespace AxonIQ.AxonServer.Connector;

// internal class ConcurrentFlowControl : IFlowControl, IDisposable
// {
//     private readonly ConcurrentBag<PooledWaiter> _waiters = new();
//     private long _requested = 0L;
//     private long _disposed = Disposed.No;
//
//     public void Request(long count)
//     {
//         if (Interlocked.Read(ref _disposed) == Disposed.No && Interlocked.Add(ref _requested, count) > 0)
//         {
//             SignalWaiters(true);
//         }
//     }
//
//     public bool TryTake()
//     {
//         if (Interlocked.Read(ref _disposed) == Disposed.Yes)
//         {
//             return false;
//         }
//         
//         if (Interlocked.Decrement(ref _requested) >= 0)
//         {
//             return true;
//         }
//
//         if (Interlocked.Increment(ref _requested) > 0)
//         {
//             SignalWaiters(true);
//         }
//         return false;
//     }
//
//     public ValueTask<bool> WaitToTakeAsync(CancellationToken cancellationToken = default)
//     {
//         if (Interlocked.Read(ref _disposed) == Disposed.Yes)
//         {
//             return ValueTask.FromResult(false);
//         }
//         
//         if (Interlocked.Read(ref _requested) > 0)
//         {
//             return ValueTask.FromResult(true);
//         }
//         
//         var waiter = PooledWaiter.Get(cancellationToken);
//         _waiters.Add(waiter);
//         
//         // REMARK: We may have been racing with .Dispose()
//         // We need to signal at least this waiter when that happens.
//         // Because we've already added the waiter we know it will be signalled.
//         if (Interlocked.Read(ref _disposed) == Disposed.Yes)
//         {
//             SignalWaiters(false);
//         }
//         
//         return new ValueTask<bool>(waiter, waiter.Version);
//     }
//     
//     public void Dispose()
//     {
//         if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
//         {
//             SignalWaiters(false);
//         }
//     }
//
//     private void SignalWaiters(bool result)
//     {
//         while (_waiters.TryTake(out var waiter))
//         {
//             waiter.SetResult(result);
//         }
//     }
//
//     private class PooledWaiter : IValueTaskSource<bool>
//     {
//         private static readonly ConcurrentStack<PooledWaiter> Pool = new();
//         
//         private CancellationTokenRegistration? _registration;
//         private ManualResetValueTaskSourceCore<bool> _source;
//
//         private PooledWaiter(CancellationToken cancellationToken)
//         {
//             _source = new ManualResetValueTaskSourceCore<bool> { RunContinuationsAsynchronously = true };
//             _registration = cancellationToken == CancellationToken.None
//                 ? null
//                 : cancellationToken.Register(version =>
//                 {
//                     try
//                     {
//                         if (_source.Version == (short)version!)
//                         {
//                             SetResult(false);
//                         }
//                     }
//                     catch (InvalidOperationException)
//                     {
//                         // ignored
//                     }
//                 }, _source.Version);
//         }
//
//         public static PooledWaiter Get(CancellationToken cancellationToken)
//         {
//             return Pool.TryPop(out var source) 
//                 ? source.WithCancellation(cancellationToken) 
//                 : new PooledWaiter(cancellationToken);
//         }
//
//         private static void Return(PooledWaiter source)
//         {
//             source.Reset();
//             Pool.Push(source);
//         }
//
//         private PooledWaiter WithCancellation(CancellationToken cancellationToken)
//         {
//             _registration =
//                 cancellationToken == CancellationToken.None
//                     ? null
//                     : cancellationToken.Register(version =>
//                     {
//                         try
//                         {
//                             if (_source.Version == (short)version!)
//                             {
//                                 SetResult(false);
//                             }
//                         }
//                         catch (InvalidOperationException)
//                         {
//                             // ignored
//                         }
//                     }, _source.Version);
//             return this;
//         }
//
//         public short Version 
//             => _source.Version;
//
//         private void Reset()
//         {
//             _source.Reset();
//             if (!_registration.HasValue) return;
//             _registration.Value.Unregister();
//             _registration.Value.Dispose();
//             _registration = null;
//         }
//
//         public void SetResult(bool result) 
//             => _source.SetResult(result);
//
//         public bool GetResult(short token)
//         {
//             var result = _source.GetResult(token);
//             Return(this);
//             return result;
//         }
//
//         public ValueTaskSourceStatus GetStatus(short token) 
//             => _source.GetStatus(token);
//
//         public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) 
//             => _source.OnCompleted(continuation, state, token, flags);
//     }
// }

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