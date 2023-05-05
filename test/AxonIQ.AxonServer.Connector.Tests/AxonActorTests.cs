using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class AxonActorUsage
{
    public class Counter : IAsyncDisposable
    {
        private readonly AxonActor<Message,State> _actor;
        private int _autoIncrementId;

        public Counter(IScheduler scheduler, ILogger<Counter> logger)
        {
            _actor = new AxonActor<Message, State>(
                Receive,
                new State(0, new Dictionary<int, TimeSpan>()),
                scheduler,
                logger);
        }

        private async Task<State> Receive(Message message, State state, CancellationToken ct)
        {
            switch (message)
            {
                case Message.Increment increment:
                    state = state with { Current = state.Current + 1 };
                    increment.Source.SetResult();
                    break;
                case Message.StartIncrementEvery start:
                    state.AutoIncrementors.Add(start.Id, start.Interval);
                    await _actor.ScheduleAsync(new Message.PeriodicIncrement(start.Id), start.Interval, ct);
                    break;
                case Message.PeriodicIncrement periodicIncrement:
                    if (state.AutoIncrementors.TryGetValue(periodicIncrement.Id, out var interval))
                    {
                        state = state with { Current = state.Current + 1 };
                        await _actor.ScheduleAsync(periodicIncrement, interval, ct);    
                    }
                    break;
                case Message.StopIncrementEvery stop:
                    state.AutoIncrementors.Remove(stop.Id);
                    break;
                case Message.Decrement decrement:
                    state = state with { Current = state.Current - 1 };
                    decrement.Source.SetResult();
                    break;
            }
            return state;
        }

        private abstract record Message
        {
            public record Increment(TaskCompletionSource Source) : Message;

            public record Decrement(TaskCompletionSource Source) : Message;

            public record StartIncrementEvery(int Id, TimeSpan Interval) : Message;

            public record PeriodicIncrement(int Id) : Message;

            public record StopIncrementEvery(int Id) : Message;
        }

        private record State(int Current, Dictionary<int, TimeSpan> AutoIncrementors);
        
        private class AutoIncrementor : IAsyncDisposable
        {
            private readonly Func<ValueTask> _onDispose;

            public AutoIncrementor(Func<ValueTask> onDispose)
            {
                _onDispose = onDispose;
            }

            public ValueTask DisposeAsync()
            {
                return _onDispose();
            }
        }

        public int Current => _actor.State.Current;
        
        public async Task IncrementAsync()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor.TellAsync(new Message.Increment(source));
            await source.Task;
        }

        public async Task<IAsyncDisposable> StartIncrementEvery(TimeSpan interval)
        {
            var id = Interlocked.Increment(ref _autoIncrementId);
            await _actor.TellAsync(new Message.StartIncrementEvery(id, interval));
            return new AutoIncrementor(() => _actor.TellAsync(new Message.StopIncrementEvery(id)));
        }
        
        public async Task DecrementAsync()
        {
            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _actor.TellAsync(new Message.Decrement(source));
            await source.Task;
        }

        public ValueTask DisposeAsync() => _actor.DisposeAsync();
    }

    [Fact]
    public async Task Show1()
    {
        var counter = CreateSystemUnderTest();

        Assert.Equal(0, counter.Current);

        await counter.IncrementAsync();
        
        Assert.Equal(1, counter.Current);

        await counter.DecrementAsync();
        
        Assert.Equal(0, counter.Current);
    }

    private static Counter CreateSystemUnderTest()
    {
        var scheduler = new Scheduler(
            () => DateTimeOffset.UtcNow,
            Scheduler.DefaultTickFrequency,
            new NullLogger<Scheduler>());
        return new Counter(scheduler, new NullLogger<Counter>());
    }

    [Fact]
    public async Task Show2()
    {
        var counter = CreateSystemUnderTest();
        
        Assert.Equal(0, counter.Current);

        await using(await counter.StartIncrementEvery(TimeSpan.FromMilliseconds(500)))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        
            Assert.True(counter.Current > 0);
        
            await Task.Delay(TimeSpan.FromSeconds(1));
        
            Assert.True(counter.Current > 2);
        }

        var current = counter.Current;
        
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        Assert.Equal(current, counter.Current);
    }
}

