using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

/// <summary>
///  Used to forward <see cref="QueryReply"/> messages to the Axon Server, when there's flow control in use and only a single query handler.
/// </summary>
internal class PassThruFlowControlledQueryReplyForwarder : IFlowControl, IAsyncDisposable
{
    private readonly QueryReplyTranslator _translator;
    private readonly ConcurrentFlowControl _flowControl;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _forwarder;

    public PassThruFlowControlledQueryReplyForwarder(Channel<QueryReply> source, WriteQueryProviderOutbound destination, QueryReplyTranslator translator)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _flowControl = new ConcurrentFlowControl();
        _cancellation = new CancellationTokenSource();
        _forwarder = Forward(source, destination, _cancellation.Token);
    }

    private async Task Forward(
        Channel<QueryReply, QueryReply> source,
        WriteQueryProviderOutbound destination,
        CancellationToken ct)
    {
        try
        {
            while (await _flowControl.WaitToTakeAsync(ct) && await source.Reader.WaitToReadAsync(ct))
            {
                var taken = _flowControl.TryTake();
                if (!taken) continue;
                var read = source.Reader.TryRead(out var reply);
                while (taken && read)
                {
                    if (reply != null)
                    {
                        foreach(var message in _translator(reply))
                        {
                            await destination(message);
                        }
                    }

                    taken = _flowControl.TryTake();
                    read = taken && source.Reader.TryRead(out reply);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }
        catch (OperationCanceledException exception) when(exception.CancellationToken == ct)
        {
            // ignored
        }
    }
    

    public void Request(long count)
    {
        _flowControl.Request(count);
    }

    public void Cancel()
    {
        _flowControl.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        _flowControl.Cancel();
        _cancellation.Cancel();
        await _forwarder;
        _cancellation.Dispose();
    }
}