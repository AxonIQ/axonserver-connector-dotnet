using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

/// <summary>
///  Used to forward <see cref="QueryReply"/> messages to the Axon Server, when there's no flow control in use and only a single query handler.
/// </summary>
internal class PassThruQueryReplyForwarder : IAsyncDisposable
{
    private readonly QueryReplyTranslator _translator;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _forwarder;
    private long _disposed = Disposed.No;

    public PassThruQueryReplyForwarder(Channel<QueryReply> from, WriteQueryProviderOutbound to, QueryReplyTranslator translator)
    {
        if (from == null) throw new ArgumentNullException(nameof(from));
        if (to == null) throw new ArgumentNullException(nameof(to));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _cancellation = new CancellationTokenSource();
        _forwarder = Forward(from, to, _cancellation.Token);
    }

    private async Task Forward(
        Channel<QueryReply, QueryReply> from,
        WriteQueryProviderOutbound to,
        CancellationToken ct)
    {
        try
        {
            while (await from.Reader.WaitToReadAsync(ct))
            {
                while (from.Reader.TryRead(out var reply))
                {
                    foreach (var message in _translator(reply))
                    {
                        await to(message);    
                    }
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
        catch (ChannelClosedException)
        {
            // ignored
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, Disposed.Yes, Disposed.No) == Disposed.No)
        {
            _cancellation.Cancel();
            await _forwarder;
            _cancellation.Dispose();
        }
    }
}