using AxonIQ.AxonServer.Connector.Tests.Framework;
using Microsoft.Extensions.Logging.Abstractions;
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
    public void ClockCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Scheduler(null!, 
                Scheduler.DefaultTickFrequency, 
                new NullLogger<Scheduler>()));
    }
    
    [Fact]
    public void LoggerCanNotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Scheduler(() => DateTimeOffset.UtcNow, 
                Scheduler.DefaultTickFrequency, 
                null!));
    }

    [Fact]
    public void FrequencyCanNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Scheduler(
                () => DateTimeOffset.UtcNow,
                TimeSpan.MinValue,
                new NullLogger<Scheduler>()));
    }
    
    [Fact]
    public async Task SchedulingTaskImmediatelyHasExpectedResult()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), _logger);

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.Zero);
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(50)));
    }
    
    [Fact]
    public async Task SchedulingCancelledTaskHasExpectedResult()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10), _logger);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source.TrySetResult();
            cancellation.Token.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(200));
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(300)));
    }
    
    [Fact]
    public async Task SchedulingTaskHasExpectedResult()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(100));
        
        Assert.True(source.Task.Wait(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task SchedulingCancelledTaskImmediatelyDoesNotInterfereWithSubsequentScheduledTasks()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source1.TrySetResult();
            cancellation.Token.ThrowIfCancellationRequested();
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
    public async Task SchedulingCancelledTaskDoesNotInterfereWithSubsequentScheduledTasks()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source1.TrySetResult();
            cancellation.Token.ThrowIfCancellationRequested();
            return ValueTask.FromCanceled(cancellation.Token);
        }, TimeSpan.FromMilliseconds(50));
        
        Assert.True(source1.Task.Wait(TimeSpan.FromMilliseconds(100)));
        
        var source2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source2.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(50));
        Assert.True(source2.Task.Wait(TimeSpan.FromMilliseconds(100)));
    }
    
    [Fact]
    public async Task SchedulingExceptionalTaskImmediatelyDoesNotInterfereWithSubsequentScheduledTasks()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);

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
    
    [Fact]
    public async Task SchedulingExceptionalTaskDoesNotInterfereWithSubsequentScheduledTasks()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10), _logger);

        var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source1.TrySetResult();
            throw new Exception();
        }, TimeSpan.FromMilliseconds(50));
        
        Assert.True(source1.Task.Wait(TimeSpan.FromMilliseconds(100)));
        
        var source2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await sut.ScheduleTaskAsync(() =>
        {
            source2.TrySetResult();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(50));
        
        Assert.True(source2.Task.Wait(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task SchedulingTaskOnDisposedSchedulerHasExpectedResult()
    {
        await using var sut = new Scheduler(() => DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(50), _logger);
        await sut.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.ScheduleTaskAsync(() => ValueTask.CompletedTask, TimeSpan.Zero).AsTask());
    }

    [Fact]
    public async Task ClockReturnsExpectedResult()
    {
        var clock = () => DateTimeOffset.UtcNow;
        await using var sut = new Scheduler(clock, TimeSpan.FromMilliseconds(50), _logger);
        Assert.Same(clock, sut.Clock);
    }
}