using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

internal class PriorityChannel<T, TPriority> : Channel<(T, TPriority)> where TPriority : struct, Enum
{
    private readonly Channel<T>[] _channels;
    private readonly TPriority[] _priorities;

    private PriorityChannel((TPriority, Channel<T>)[] channels)
    {
        if (channels == null) throw new ArgumentNullException(nameof(channels));
        
        _priorities = channels.Select(item => item.Item1).ToArray();
        _channels = channels.Select(item => item.Item2).ToArray();
        
        Reader = new PriorityChannelReader(this);
        Writer = new PriorityChannelWriter(this);
    }

    public static PriorityChannel<T, TPriority> CreateUnbounded(UnboundedChannelOptions options)
    {
        return new PriorityChannel<T, TPriority>(
            Enum.GetValues<TPriority>()
                .OrderByDescending(priority => priority)
                .Select(priority => (priority, Channel.CreateUnbounded<T>(options)))
                .ToArray()
        );
    }
    
    private async ValueTask<bool> WaitAsync(Func<Channel<T>, CancellationToken, ValueTask<bool>> action, CancellationToken cancellationToken = default)
    {
        var exceptions = new HashSet<Exception>();
        var channels = new List<Channel<T>>(_channels);
        while (channels.Count > 0)
        {
            var waiters = channels
                .Select(channel => action(channel, cancellationToken).AsTask())
                .ToArray();
            var waiter = await Task.WhenAny(waiters);
            if (waiter.IsCompletedSuccessfully)
            {
                // Only if the WaitToRead|WriteAsync operation returns true of any channel, do we return true.
                // This means it's okay to try read from or write to "some" of the channels. Since we only use
                // WaitToReadAsync operation, it's fine to try and read each channel.
                if (await waiter)
                {
                    return true;
                }
            }
            else if(waiter.IsCanceled)
            {
                // A wait operation only ever cancels if this token caused it.
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                try
                {
                    await waiter;
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);    
                }
            }
            channels.RemoveAt(Array.IndexOf(waiters, waiter));
        }

        // We propagate any exceptions that occurred during the WaitToRead|WriteAsync operation.
        if (exceptions.Count != 0)
        {
            throw exceptions.Count switch
            {
                1 => exceptions.Single(),
                _ => new AggregateException(exceptions.ToArray())
            };
        }

        return false;
    }

    private sealed class PriorityChannelWriter : ChannelWriter<(T, TPriority)>
    {
        private readonly PriorityChannel<T, TPriority> _parent;

        public PriorityChannelWriter(PriorityChannel<T, TPriority> parent)
        {
            _parent = parent;
        }

        public override bool TryComplete(Exception? error = null)
        {
            var completions = Array.ConvertAll(_parent._channels, channel => channel.Writer.TryComplete(error));
            return Array.TrueForAll(completions, completion => completion);
        }

        public override bool TryWrite((T, TPriority) item)
        {
            var index = Array.IndexOf(_parent._priorities, item.Item2);
            return index != -1 && _parent._channels[index].Writer.TryWrite(item.Item1);
        }

        public override ValueTask WriteAsync((T, TPriority) item, CancellationToken cancellationToken = default)
        {
            var index = Array.IndexOf(_parent._priorities, item.Item2);
            return index != -1
                ? _parent._channels[index].Writer.WriteAsync(item.Item1, cancellationToken) 
                : ValueTask.CompletedTask;
        }

        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
        {
            return _parent.WaitAsync((channel, ct) => channel.Writer.WaitToWriteAsync(ct), cancellationToken);
        }
    }

    private sealed class PriorityChannelReader : ChannelReader<(T, TPriority)>
    {
        private readonly PriorityChannel<T, TPriority> _parent;
        
        public PriorityChannelReader(PriorityChannel<T, TPriority> parent)
        {
            _parent = parent;
            
            CanCount = _parent._channels.Aggregate(true, (current, channel) => current && channel.Reader.CanCount);
            CanPeek = _parent._channels.Aggregate(true, (current, channel) => current && channel.Reader.CanPeek);
            Completion = Task.WhenAll(_parent._channels.Select(channel => channel.Reader.Completion));
        }

        public override bool CanCount { get; }

        public override int Count => 
            _parent._channels.Aggregate(0, (current, channel) => current + channel.Reader.Count);

        public override bool CanPeek { get; }

        public override Task Completion { get; }

        public override bool TryPeek(out (T, TPriority) item)
        {
            for (var index = 0; index < _parent._channels.Length; index++)
            {
                if (!_parent._channels[index].Reader.TryPeek(out var peeked)) continue;
                item = (peeked, _parent._priorities[index]);
                return true;
            }

            item = default;
            return false;
        }

        public override bool TryRead(out (T, TPriority) item)
        {
            for (var index = 0; index < _parent._channels.Length; index++)
            {
                if (!_parent._channels[index].Reader.TryRead(out var read)) continue;
                item = (read, _parent._priorities[index]);
                return true;
            }

            item = default;
            return false;
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            return _parent.WaitAsync((channel, ct) => channel.Reader.WaitToReadAsync(ct), cancellationToken);
        }
    }
}