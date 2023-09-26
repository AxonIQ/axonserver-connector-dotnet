using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

internal static class ChannelToActorExtensions
{
    public static Task TellQueryRepliesToAsync<TOutput>(
        this Channel<QueryReply> source, 
        IAxonPriorityActor<TOutput> destination, 
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator, 
        CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (translator == null) throw new ArgumentNullException(nameof(translator));
        
        return TellQueryRepliesToAsyncCore(source, destination, translator, cancellationToken);
    }

    private static async Task TellQueryRepliesToAsyncCore<TOutput>(
        ChannelReader<QueryReply> source, 
        IAxonPriorityActor<TOutput> destination,
        Func<QueryReply, IReadOnlyCollection<TOutput>> translator, 
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
        catch (ObjectDisposedException)
        {
            // ignore
        }
        catch (ChannelClosedException)
        {
            // ignore
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken)
        {
            // ignore
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