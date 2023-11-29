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
}