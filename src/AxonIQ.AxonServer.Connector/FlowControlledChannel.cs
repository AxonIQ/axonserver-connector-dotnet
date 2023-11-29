using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

internal class FlowControlledChannel<T> : Channel<T>
{
    private readonly ConcurrentFlowControl _flowControl;
    private readonly Channel<T> _channel;

    public FlowControlledChannel(ConcurrentFlowControl flowControl, Channel<T> channel)
    {
        _flowControl = flowControl ?? throw new ArgumentNullException(nameof(flowControl));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        
        Reader = new FlowControlledChannelReader(this);
        Writer = new FlowControlledChannelWriter(this);
    }

    private sealed class FlowControlledChannelReader(FlowControlledChannel<T> parent) : ChannelReader<T>
    {
        public override Task Completion => parent.Reader.Completion;
        public override int Count => parent.Reader.Count;
        public override bool CanCount => parent.Reader.CanCount;
        public override bool CanPeek => parent.Reader.CanPeek;
        public override bool TryPeek([MaybeNullWhen(false)] out T item) => parent.Reader.TryPeek(out item);

        public override bool TryRead([MaybeNullWhen(false)] out T item)
        {
            if(parent._flowControl.TryTake())
            {
                if(parent._channel.Reader.TryRead(out item)) return true;
                parent._flowControl.Request(1);
            }

            item = default;
            return false;
        }

        public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            if (await parent._flowControl.WaitToTakeAsync(cancellationToken))
            {
                return await parent._channel.Reader.WaitToReadAsync(cancellationToken);
            }
            return false;
        }
    }
    
    private sealed class FlowControlledChannelWriter(FlowControlledChannel<T> parent) : ChannelWriter<T>
    {
        public override bool TryComplete(Exception? error = null) => parent.Writer.TryComplete(error);
        public override bool TryWrite(T item) => parent.Writer.TryWrite(item);
        public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default) =>
            parent.Writer.WaitToWriteAsync(cancellationToken);
        public override ValueTask WriteAsync(T item, CancellationToken cancellationToken = default) =>
            parent.Writer.WriteAsync(item, cancellationToken);
    }
}