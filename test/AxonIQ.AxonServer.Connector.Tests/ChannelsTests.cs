using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ChannelsTests
{
    [Fact]
    public void BoundedJoinAllChannelCompletesWhenNoChannelsToJoin()
    {
        var channels = Array.Empty<Channel<object>>();
        var joined = Channels.BoundedJoinAll(channels, Random.Shared.Next(1, 100));
        Assert.Equal(0, joined.Reader.Count);
        Assert.False(joined.Writer.TryComplete());
    }
    
    [Fact]
    public async Task BoundedJoinAllChannelCompletesWhenAllChannelsJoinedAreCompleted()
    {
        var channels = Enumerable
            .Range(0, Random.Shared.Next(1, 5))
            .Select(_ => Channel.CreateUnbounded<Message>())
            .ToArray();

        var messages = new List<Message>();
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            foreach(var item in Enumerable.Range(0, Random.Shared.Next(1, 5)))
            {
                var message = new Message(index, item);
                if (channel.Writer.TryWrite(message))
                {
                    messages.Add(message);
                }
            }
            channel.Writer.Complete();
        }
        var joined = Channels.BoundedJoinAll(channels, Random.Shared.Next(1, 5));
        var actual = await joined.Reader.ReadAllAsync().ToListAsync();
        Assert.Equal(new HashSet<Message>(messages), new HashSet<Message>(actual));
    }

    private record Message(int ChannelId, int Item);
}