using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;
using Microsoft.Extensions.Logging;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelToActorExtensions
{
    public static Task TellQueryRepliesToAsync<TOutput>(
        this Channel<QueryReply> source, 
        IAxonPriorityActor<TOutput> destination, 
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator, 
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (translator == null) throw new ArgumentNullException(nameof(translator));
        
        return TellQueryRepliesToAsyncCore(source.Reader, destination, translator, logger, cancellationToken);
    }

    private static async Task TellQueryRepliesToAsyncCore<TOutput>(
        ChannelReader<QueryReply> source,
        IAxonPriorityActor<TOutput> destination,
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var completedAtLeastOnceWithoutErrors = false;
        var errors = new List<ErrorMessage>();
        try
        {
            while (await source.WaitToReadAsync(cancellationToken))
            {
                while (source.TryRead(out var item))
                {
                    switch (item)
                    {
                        case QueryReply.Send send:
                            foreach (var message in translator(send))
                            {
                                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);
                            }
                            break;
                        case QueryReply.CompleteWithError completion:
                            // aggregated because we only send it once at the end
                            errors.Add(completion.Error);
                            break;
                        case QueryReply.Complete: 
                            // ignored because we only send it once at the end
                            completedAtLeastOnceWithoutErrors = true;
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
        if(!completedAtLeastOnceWithoutErrors && errors.Count != 0)
        {
            foreach (var message in translator(new QueryReply.CompleteWithError(errors[0])))
            {
                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);    
            }
        }
        else
        {
            foreach (var message in translator(new QueryReply.Complete()))
            {
                await destination.TellAsync(MessagePriority.Secondary, message, cancellationToken);    
            }
        }
    }
}