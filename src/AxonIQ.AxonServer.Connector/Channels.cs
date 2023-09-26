using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector;

internal static class Channels
{
    public static Channel<T> CreateBounded<T>(int capacity)
    {
        return Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    }
    
    public static Channel<T> BoundedJoinAll<T>(IReadOnlyCollection<Channel<T>> channels, int capacity)
    {
        var joined = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            AllowSynchronousContinuations = false,
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        switch (channels.Count)
        {
            case 0:
                joined.Writer.Complete();
                break;
            case 1:
                Task.Run(async () =>
                {
                    await channels.Single().PipeTo(joined);
                    joined.Writer.Complete();
                });
                break;
            default:
                Task.Run(async () =>
                {
                    await Task.WhenAll(channels.Select(channel => channel.PipeTo(joined)));
                    joined.Writer.Complete();
                });
                break;
        }

        return joined;
    }
}