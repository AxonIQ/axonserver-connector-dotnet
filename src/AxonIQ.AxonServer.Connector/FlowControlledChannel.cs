using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

internal class FlowControlledChannel<T> : Channel<T>
{
    private readonly ConcurrentFlowControl _flowControl;
    private readonly Channel<T> _channel;
    
    public FlowControlledChannel(ConcurrentFlowControl flowControl)
    {
        _flowControl = flowControl ?? throw new ArgumentNullException(nameof(flowControl));
        _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        Reader = _channel.Reader; // new FlowControlledChannelReader(this);
        Writer = new FlowControlledChannelWriter(this);
    }
    
    private sealed class FlowControlledChannelReader : ChannelReader<T>
    {
        private readonly FlowControlledChannel<T> _parent;

        public FlowControlledChannelReader(FlowControlledChannel<T> parent)
        {
            _parent = parent;
        }

        public override Task Completion => _parent._channel.Reader.Completion;
        public override int Count => _parent._channel.Reader.Count;
        public override bool CanCount => _parent._channel.Reader.CanCount;
        public override bool CanPeek => _parent._channel.Reader.CanPeek;

        public override bool TryRead(out T item)
        {
            return _parent._channel.Reader.TryRead(out item!);
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            return _parent._channel.Reader.WaitToReadAsync(cancellationToken);
        }
    }
    
    private sealed class FlowControlledChannelWriter : ChannelWriter<T>
    {
        private readonly FlowControlledChannel<T> _parent;

        public FlowControlledChannelWriter(FlowControlledChannel<T> parent)
        {
            _parent = parent;
        }

        public override bool TryComplete(Exception? error = null) => _parent._channel.Writer.TryComplete(error);

        public override bool TryWrite(T item)
        {
            if (_parent._flowControl.TryTake())
            {
                if (_parent._channel.Writer.TryWrite(item)) return true;
                // NOTE: return the one we've taken but were not able to write
                _parent._flowControl.Request(1);
            }
            return false;
        }

        public override async ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
        {
            if (await _parent._flowControl.WaitToTakeAsync(cancellationToken))
            {
                return await _parent._channel.Writer.WaitToWriteAsync(cancellationToken);
            }

            return false;
        }
    }
}