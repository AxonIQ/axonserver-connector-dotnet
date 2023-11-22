using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

/// <summary>
///  Used to forward <see cref="QueryReply"/> messages to the Axon Server, when there's flow control in use and multiple query handlers.
/// </summary>
internal class FlowControlledQueryReplyForwarder : IFlowControl, IAsyncDisposable
{
    private readonly QueryReplyTranslator _translator;
    private readonly ConcurrentFlowControl _flowControl;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _forwarder;

    public FlowControlledQueryReplyForwarder(Channel<QueryReply> source, WriteQueryProviderOutbound destination, QueryReplyTranslator translator)
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
            var errors = new List<ErrorMessage>();
            while (await _flowControl.WaitToTakeAsync(ct) && await source.Reader.WaitToReadAsync(ct))
            {
                var taken = _flowControl.TryTake();
                if (taken)
                {
                    var read = source.Reader.TryRead(out var reply);
                    while (taken && read)
                    {
                        if (reply != null)
                        {
                            switch (reply)
                            {
                                case QueryReply.Send send:
                                    foreach(var message in _translator(send))
                                    {
                                        await destination(message);
                                    }
                                    break;
                                case QueryReply.CompleteWithError complete:
                                    // REMARK: We remember all errors and send one at the end
                                    errors.Add(complete.Error);
                                    break;
                                // REMARK: QueryReply.Complete gets skipped since we want to control the completion ourselves
                            }
                        }

                        taken = _flowControl.TryTake();
                        read = taken && source.Reader.TryRead(out reply);
                    }
                }
            }

            if (errors.Count > 0)
            {
                // REMARK: Complete with the first error
                foreach(var message in _translator(new QueryReply.CompleteWithError(errors[0])))
                {
                    await destination(message);
                }
                
                // TODO: We may want to log the other errors?
            }
            else
            {
                // REMARK: Complete without errors
                foreach(var message in _translator(new QueryReply.Complete()))
                {
                    await destination(message);
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
        _cancellation.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        _flowControl.Cancel();
        _cancellation.Cancel();
        await _forwarder;
        _cancellation.Dispose();
    }
}