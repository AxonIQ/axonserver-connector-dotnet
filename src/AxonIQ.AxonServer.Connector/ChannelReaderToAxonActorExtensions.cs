using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelReaderToAxonActorExtensions
{
    public static Task TellToAsync<T, TMessage>(this ChannelReader<T> reader,
        IAxonActor<TMessage> actor,
        Func<TaskResult<T>, TMessage> translate,
        int count,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (translate == null) throw new ArgumentNullException(nameof(translate));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        
        return TellToCore(
            reader, 
            actor,
            count, 
            value => translate(new TaskResult<T>.Ok(value)), 
            exception => translate(new TaskResult<T>.Error(exception)), 
            logger, ct);
    }
    
    private static async Task TellToCore<T, TMessage>(ChannelReader<T> reader,
        IAxonActor<TMessage> actor,
        int count,
        Func<T, TMessage> success,
        Func<Exception, TMessage> failure,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var read = 0;
            while (read < count && await reader.WaitToReadAsync(ct))
            {
                while (read < count && reader.TryRead(out var response))
                {
                    if (response != null)
                    {
                        await actor
                            .TellAsync(success(response), ct)
                            .ConfigureAwait(false);
                    }

                    read++;
                }
            }
        }
        catch (OperationCanceledException exception)
        {
            // If the caller did not cancel the consumer via the provided cancellation token,
            // the suspicion is that it was cancelled due to underlying connectivity issues.
            if (exception.CancellationToken != ct)
            {
                logger.LogWarning(exception,
                    "The channel stream is no longer being read because an operation was cancelled");
                
                await actor
                    .TellAsync(failure(exception), ct)
                    .ConfigureAwait(false);
            }
            else
            {
                logger.LogDebug(exception,
                    "The channel stream is no longer being read because an operation was cancelled");
            }
        }
        catch (ObjectDisposedException exception)
        {
            logger.LogDebug(exception,
                "The channel stream is no longer being read because an object got disposed");
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                exception,
                "The channel stream is no longer being read because of an unexpected exception");
            
            await actor
                .TellAsync(failure(exception), ct)
                .ConfigureAwait(false);
        }
    }
}