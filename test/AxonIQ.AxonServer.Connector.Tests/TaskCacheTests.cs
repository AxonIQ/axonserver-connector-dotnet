using AxonIQ.AxonServer.Connector.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

public class TaskCacheTests
{
    private readonly TestOutputHelperLogger<Scheduler> _logger;

    public TaskCacheTests(ITestOutputHelper output)
    {
        _logger = new TestOutputHelperLogger<Scheduler>(output);
    }

    [Fact]
    public void AddTaskWithUnknownTokenHasExpectedResult()
    {
        var sut = new TaskCache(() => DateTimeOffset.UtcNow);
        var token = TaskCache.Token.Next();
        sut.Add(token, Task.CompletedTask);
    }
    
    [Fact]
    public void AddTaskWithKnownTokenHasExpectedResult()
    {
        var sut = new TaskCache(() => DateTimeOffset.UtcNow);
        var token = TaskCache.Token.Next();
        sut.Add(token, Task.CompletedTask);
        Assert.Throws<ArgumentException>(() => sut.Add(token, Task.CompletedTask));
    }
    
    [Fact]
    public void TryRemoveWithKnownTokenHasExpectedResult()
    {
        var sut = new TaskCache(() => DateTimeOffset.UtcNow);
        var token = TaskCache.Token.Next();
        sut.Add(token, Task.CompletedTask);
        Assert.True(sut.TryRemove(token, out var task));
        Assert.Same(Task.CompletedTask, task);
    }
    
    [Fact]
    public void TryRemoveWithUnknownTokenHasExpectedResult()
    {
        var sut = new TaskCache(() => DateTimeOffset.UtcNow);
        var token = TaskCache.Token.Next();
        Assert.False(sut.TryRemove(token, out var task));
        Assert.Same(Task.CompletedTask, task);
    }
    
    [Fact]
    public void PurgeWithDueTasksHasExpectedResult()
    {
        var time = DateTimeOffset.UtcNow;
        var callCount = 0;
        var clock = () =>
        {
            if (callCount >= 2) return time.Add(TimeSpan.FromMilliseconds(1));
            callCount++;
            return time;
        };
            
        var sut = new TaskCache(clock);
        sut.Add(TaskCache.Token.Next(), Task.CompletedTask);
        sut.Add(TaskCache.Token.Next(), Task.CompletedTask);
        
        var actual = sut.Purge(TimeSpan.Zero);
        
        Assert.Equal(new[] { Task.CompletedTask, Task.CompletedTask}, actual);
    }
    
    [Fact]
    public void PurgeWithoutDueTasksHasExpectedResult()
    {
        var time = DateTimeOffset.UtcNow;
        var callCount = 0;
        var clock = () =>
        {
            if (callCount >= 2) return time.Subtract(TimeSpan.FromMilliseconds(1));
            callCount++;
            return time;
        };
            
        var sut = new TaskCache(clock);
        sut.Add(TaskCache.Token.Next(), Task.CompletedTask);
        sut.Add(TaskCache.Token.Next(), Task.CompletedTask);
        
        var actual = sut.Purge(TimeSpan.Zero);
        
        Assert.Empty(actual);
    }
}