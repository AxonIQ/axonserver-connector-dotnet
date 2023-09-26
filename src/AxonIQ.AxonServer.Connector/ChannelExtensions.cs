using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelExtensions
{
    public static async Task PipeTo<T>(this Channel<T> source, Channel<T> destination, CancellationToken cancellationToken = default)
    {
        try
        {
            while (await source.Reader.WaitToReadAsync(cancellationToken))
            {
                while (source.Reader.TryRead(out var item))
                {
                    await destination.Writer.WriteAsync(item, cancellationToken);
                }
            }
        }
        catch (ChannelClosedException)
        {
            // ignore
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
        {
            // ignore
        }
    }
    
    public static Task ReadFromAll<T>(this Channel<T> destination, IReadOnlyCollection<Channel<T>> sources, CancellationToken ct)
    {
        return sources.Count switch
        {
            0 => Task.Run(() => destination.Writer.Complete(), ct),
            1 => Task.Run(async () =>
            {
                await sources.Single().PipeTo(destination, ct);
                destination.Writer.Complete();
            }, ct),
            _ => Task.Run(async () =>
            {
                await Task.WhenAll(sources.Select(channel => channel.PipeTo(destination, ct)));
                destination.Writer.Complete();
            }, ct)
        };
    }
}