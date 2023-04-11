using AxonIQ.AxonServer.Connector.Tests.Framework;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests;

public class SchedulerTests
{
    private readonly TestOutputHelperLogger<Scheduler> _logger;

    public SchedulerTests(ITestOutputHelper output)
    {
        _logger = new TestOutputHelperLogger<Scheduler>(output);
    }
    
    [Fact]
    public async Task SchedulingTaskImmediatelyHasExpectedResult()
    {
        var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), _logger);

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero);
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(50)));
    }
    
    [Fact]
    public async Task SchedulingTaskHasExpectedResult()
    {
        var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(100));
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task SchedulingCancelledTaskDoesNotInterfereWithSubsequentScheduledTasks()
    {
        var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source1.TrySetResult();
            return ValueTask.FromCanceled(cancellation.Token);
        }, TimeSpan.Zero);
        
        Assert.True(source1.Task.Wait(TimeSpan.FromMilliseconds(100)));
        
        var source2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source2.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero);
        Assert.True(source2.Task.Wait(TimeSpan.FromMilliseconds(100)));
    }
    
    [Fact]
    public async Task SchedulingExceptionalTaskDoesNotInterfereWithSubsequentScheduledTasks()
    {
        var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source1.TrySetResult();
            throw new Exception();
        }, TimeSpan.Zero);
        
        Assert.True(source1.Task.Wait(TimeSpan.FromMilliseconds(100)));
        
        var source2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source2.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero);
        
        Assert.True(source2.Task.Wait(TimeSpan.FromMilliseconds(100)));
    }
}