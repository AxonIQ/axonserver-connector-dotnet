using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelExtensions
{
    public static async Task PipeTo<T>(this Channel<T> source, Channel<T> destination, ILogger logger, CancellationToken cancellationToken = default)
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
            logger.LogDebug("Piping messages from source to destination completed gracefully");
        }
        catch (ChannelClosedException)
        {
            // ignore
            logger.LogDebug("Channel was closed while piping messages from source to destination");
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
        {
            // ignore
            logger.LogDebug("Operation was cancelled while piping messages from source to destination");
        }
    }
    
    public static Task PipeFromAll<T>(this Channel<T> destination, IReadOnlyCollection<Channel<T>> sources, ILogger logger, CancellationToken ct)
    {
        return sources.Count switch
        {
            0 => Task.Run(() =>
            {
                logger.LogDebug("No sources to pipe to destination from");
                destination.Writer.Complete();
            }, ct),
            1 => Task.Run(async () =>
            {
                logger.LogDebug("One source to pipe to destination from");
                await sources.Single().PipeTo(destination, logger, ct);
                destination.Writer.Complete();
            }, ct),
            _ => Task.Run(async () =>
            {
                logger.LogDebug("Many sources to pipe to destination from");
                await Task.WhenAll(sources.Select(channel => channel.PipeTo(destination, logger, ct)));
                destination.Writer.Complete();
            }, ct)
        };
    }
}