using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelToActorExtensions
{
    public static Task TellQueryRepliesToAsync<TOutput>(
        this Channel<QueryReply> source, 
        IAxonPriorityActor<TOutput> destination,
        int expectedCompletionCount,
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator, 
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (translator == null) throw new ArgumentNullException(nameof(translator));
        
        return TellQueryRepliesToAsyncCore(source.Reader, destination, expectedCompletionCount, translator, logger, cancellationToken);
    }

    private static async Task TellQueryRepliesToAsyncCore<TOutput>(
        ChannelReader<QueryReply> source,
        IAxonPriorityActor<TOutput> destination,
        int expectedCompletionCount,
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // This code works on the assumption that the source channel reports completion only once per ChannelId
        // Hence, it knows we're done when all channels have reported completion (with or without error)
        var completed = new List<ChannelId>();
        var completedWithError = new List<(ChannelId, ErrorMessage)>();
        try
        {
            while (completed.Count + completedWithError.Count < expectedCompletionCount && 
                   await source.WaitToReadAsync(cancellationToken))
            {
                while (source.TryRead(out var item))
                {
                    switch (item)
                    {
                        case QueryReply.Send send:
                            logger.LogDebug("Translating query reply from channel {ChannelId} and sending as outbound messages: {Response}", send.Id.ToString(), send.Response);
                            foreach (var message in translator(send))
                            {
                                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);
                            }
                            break;
                        case QueryReply.CompleteWithError completion:
                            logger.LogDebug("Tracking query completion from channel {ChannelId} with error: {Error}", completion.Id.ToString(), completion.Error);
                            // aggregated because we only send it once at the end
                            completedWithError.Add((completion.Id, completion.Error));
                            break;
                        case QueryReply.Complete completion:
                            logger.LogDebug("Tracking query completion from channel {ChannelId}", completion.Id.ToString());
                            // ignored because we only send it once at the end
                            completed.Add(completion.Id);
                            break;
                    }
                }
            }
        }
        catch (ObjectDisposedException exception)
        {
            // ignore
            logger.LogDebug("Object named {ObjectName} was disposed while telling query replies to query channel", exception.ObjectName);
        }
        catch (ChannelClosedException)
        {
            // ignore
            logger.LogDebug("Channel was closed while telling query replies to query channel");
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
        {
            // ignore
            logger.LogDebug("Operation was cancelled while telling query replies to query channel");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Unexpected exception while telling query replies to query channel");
        }
        if(completed.Count == 0 && completedWithError.Count != 0)
        {
            logger.LogDebug("Sending query completion with error outbound messages");
            // We report the first error only
            var (id, error) = completedWithError[0];
            foreach (var message in translator(new QueryReply.CompleteWithError(id, error)))
            {
                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);    
            }
        }
        else
        {
            // We report successful completion even if there were errors
            logger.LogDebug("Sending query completion outbound messages");
            var id = completed[0];
            foreach (var message in translator(new QueryReply.Complete(id)))
            {
                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);    
            }
        }
    }
}