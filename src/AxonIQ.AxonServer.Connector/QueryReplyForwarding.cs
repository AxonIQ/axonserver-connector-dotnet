using System.Threading.Channels;
using Io.Axoniq.Axonserver.Grpc;

namespace AxonIQ.AxonServer.Connector;

internal static class QueryReplyForwarding
{
    public static async Task ForSingleHandler(
        this Channel<QueryReply> source,
        WriteQueryProviderOutbound destination,
        ConcurrentFlowControl flowControl, 
        QueryReplyTranslator translator,
        CancellationToken ct)
    {
        try
        {
            while (await flowControl.WaitToTakeAsync(ct) && await source.Reader.WaitToReadAsync(ct))
            {
                var taken = flowControl.TryTake();
                if (!taken) continue;
                var read = source.Reader.TryRead(out var reply);
                while (taken && read)
                {
                    if (reply != null)
                    {
                        foreach(var message in translator(reply))
                        {
                            await destination(message);
                        }
                    }

                    taken = flowControl.TryTake();
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
    
    public static async Task ForManyHandlers(
        this Channel<QueryReply> source,
        WriteQueryProviderOutbound destination,
        ConcurrentFlowControl flowControl, 
        QueryReplyTranslator translator,
        CancellationToken ct)
    {
        try
        {
            var errors = new List<ErrorMessage>();
            while (await flowControl.WaitToTakeAsync(ct) && await source.Reader.WaitToReadAsync(ct))
            {
                var taken = flowControl.TryTake();
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
                                    foreach(var message in translator(send))
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

                        taken = flowControl.TryTake();
                        read = taken && source.Reader.TryRead(out reply);
                    }
                }
            }

            if (errors.Count > 0)
            {
                // REMARK: Complete with the first error
                foreach(var message in translator(new QueryReply.CompleteWithError(errors[0])))
                {
                    await destination(message);
                }
                
                // TODO: We may want to log the other errors?
            }
            else
            {
                // REMARK: Complete without errors
                foreach(var message in translator(new QueryReply.Complete()))
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
}