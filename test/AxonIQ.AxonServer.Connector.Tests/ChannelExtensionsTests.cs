using System.Threading.Channels;

namespace AxonIQ.AxonServer.Connector.Tests;

public class ChannelExtensionsTests
{
    [Fact]
    public async Task PipeToPipesAllItemsFromSourceToDestination()
    {
        var source = Channel.CreateUnbounded<int>();
        var destination = Channel.CreateUnbounded<int>();
        var count = Random.Shared.Next(2,5);
        var items = Enumerable.Range(0, count).ToArray();
        foreach (var item in items)
        {
            source.Writer.TryWrite(item);
        }
        source.Writer.Complete();
        await source.PipeTo(destination);
        var actual = await destination.Reader.ReadAllAsync().Take(count).ToArrayAsync();
        Assert.Equal(items, actual);
    }
    
    [Fact]
    public async Task PipeToPipesNoItemsFromSourceToDestinationWhenDestinationIsClosed()
    {
        var source = Channel.CreateUnbounded<int>();
        var destination = Channel.CreateUnbounded<int>();
        destination.Writer.Complete();
        var count = Random.Shared.Next(2,5);
        var items = Enumerable.Range(0, count).ToArray();
        foreach (var item in items)
        {
            source.Writer.TryWrite(item);
        }
        source.Writer.Complete();
        await source.PipeTo(destination);
        var actual = await destination.Reader.ReadAllAsync().Take(count).ToArrayAsync();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task PipeToPipesAllItemsFromSourceToDestinationIncrementally()
    {
        var source = Channel.CreateUnbounded<int>();
        var destination = Channel.CreateUnbounded<int>();
        
        var pipe = source.PipeTo(destination);
        
        var count = Random.Shared.Next(100,200);
        var items = Enumerable.Range(0, count).ToArray();
        var index = 0; 
        foreach (var item in items)
        {
            if (index > 0 && index % 10 == 0)
            {
                Assert.False(pipe.IsCompleted);
                
                var actual = await destination.Reader.ReadAllAsync().Take(10).ToArrayAsync();
                Assert.Equal(Enumerable.Range(index - 10, 10).ToArray(), actual);
            }
            source.Writer.TryWrite(item);
            index++;
        }
        source.Writer.Complete();
        
        await pipe;

        var remainder = index % 10;
        if (remainder != 0)
        {
            var actualRemainder = await destination.Reader.ReadAllAsync().Take(remainder).ToArrayAsync();
            Assert.Equal(Enumerable.Range(index - remainder, remainder).ToArray(), actualRemainder);
        }
    }
    
    [Fact]
    public async Task PipeToHandlesCancelledCancellationTokenGracefully()
    {
        var source = Channel.CreateUnbounded<int>();
        var destination = Channel.CreateUnbounded<int>();
        var count = Random.Shared.Next(100,200);
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var pipe = source.PipeTo(destination, cancellationTokenSource.Token);
        var items = Enumerable.Range(0, count).ToHashSet();
        foreach (var item in items)
        {
            source.Writer.TryWrite(item);
        }
        source.Writer.Complete();
        await pipe;
        destination.Writer.Complete();
        var actual = await destination.Reader.ReadAllAsync().Take(10).ToArrayAsync();
        Assert.Empty(actual);
    }
    
    [Fact]
    public async Task PipeToHandlesCancellationOfTokenGracefully()
    {
        var source = Channel.CreateUnbounded<int>();
        var destination = Channel.CreateUnbounded<int>();
        var count = Random.Shared.Next(100,200);
        var cancellationTokenSource = new CancellationTokenSource();
        var pipe = source.PipeTo(destination, cancellationTokenSource.Token);
        var items = Enumerable.Range(0, count).ToHashSet();
        var pointOfCancellation = Random.Shared.Next(50,100);
        var index = 0;
        foreach (var item in items)
        {
            if (index == pointOfCancellation)
            {
                cancellationTokenSource.Cancel();
            }
            source.Writer.TryWrite(item);
            index++;
        }
        source.Writer.Complete();
        await pipe;
        destination.Writer.Complete();
        var actual = await destination.Reader.ReadAllAsync().Take(10).ToArrayAsync();
        Assert.Subset(items, new HashSet<int>(actual));
    }
}