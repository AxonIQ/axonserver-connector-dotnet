using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

/// <summary>
///  Used to forward <see cref="QueryReply"/> messages to the Axon Server, when there's no flow control in use and multiple query handlers.
/// </summary>
internal class QueryReplyForwarder : IAsyncDisposable
{
    private readonly QueryReplyTranslator _translator;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _forwarder;

    public QueryReplyForwarder(Channel<QueryReply> source, WriteQueryProviderOutbound destination, QueryReplyTranslator translator)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
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
            while (await source.Reader.WaitToReadAsync(ct))
            {
                while (source.Reader.TryRead(out var reply))
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

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        await _forwarder;
        _cancellation.Dispose();
    }
}