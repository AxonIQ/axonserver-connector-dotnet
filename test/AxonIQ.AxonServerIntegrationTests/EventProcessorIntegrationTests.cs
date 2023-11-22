using AutoFixture;
using AxonIQ.AxonServer.Connector;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using AxonIQ.AxonServerIntegrationTests.Containerization;
using Io.Axoniq.Axonserver.Grpc.Admin;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using AsyncEnumerable = System.Linq.AsyncEnumerable;

namespace AxonIQ.AxonServerIntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
[Trait("Surface", "ControlChannel")]
public class EventProcessorIntegrationTests
{
    private readonly IAxonServer _container;
    private readonly Fixture _fixture;
    private readonly ILoggerFactory _loggerFactory;

    public EventProcessorIntegrationTests(AxonServerWithAccessControlDisabled container, ITestOutputHelper output)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _fixture = new Fixture();
        _fixture.CustomizeClientInstanceId();
        _fixture.CustomizeComponentName();
        _fixture.CustomizeEventProcessorName();
        _fixture.CustomizeTokenStoreIdentifier();
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }
    
    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectorOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectorOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }

    [Fact]
    public async Task RegisterEventProcessorCausesServerToObserveEventProcessorInfo()
    {
        var sut = await CreateSystemUnderTest();
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        var admin = sut.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var id = _fixture.Create<TokenStoreIdentifier>();
        Func<Task<EventProcessorInfo?>> supplier = () =>
        {
            return Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
            {
                Running = true,
                AvailableThreads = 1,
                ActiveThreads = 1,
                Error = false,
                IsStreamingProcessor = false,
                Mode = "Tracking",
                ProcessorName = name.ToString(),
                TokenStoreIdentifier = id.ToString()
            });
        };
        
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompletedAsync();

        // Allow Axon Server to learn about this event processor
        await Task.Delay(TimeSpan.FromSeconds(2));

        var actual = await admin
            .GetEventProcessorsByComponent(sut.ClientIdentity.ComponentName)
            .SingleAsync();
            
        Assert.Equal(name.ToString(), actual.Identifier.ProcessorName);
    }
    
    [Fact]
    public async Task RegisterEventProcessorCausesInitialInfoPollToHappen()
    {
        var sut = await CreateSystemUnderTest();
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        //var admin = sut.AdminChannel;
        var name = _fixture.Create<EventProcessorName>();
        var completion = new TaskCompletionSource();
        Func<Task<EventProcessorInfo?>> supplier = () =>
        {
            completion.SetResult();
            return Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
            {
                Running = true,
                AvailableThreads = 1,
                ActiveThreads = 1,
                Error = false,
                IsStreamingProcessor = false,
                Mode = "Tracking",
                ProcessorName = name.ToString()
            });
        };
        
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompletedAsync();

        //Allow the poll to happen
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RegisterEventProcessorCausesPeriodicInfoPollToHappen()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(100)));
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        
        var name = _fixture.Create<EventProcessorName>();
        var callCount = 0;
        var completion = new TaskCompletionSource();
        Func<Task<EventProcessorInfo?>> supplier = () =>
        {
            callCount++;
            if (callCount == 2)
            {
                completion.SetResult();    
            }
            return Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
            {
                Running = true,
                AvailableThreads = 1,
                ActiveThreads = 1,
                Error = false,
                IsStreamingProcessor = false,
                Mode = "Tracking",
                ProcessorName = name.ToString()
            });
        };
        
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompletedAsync();

        //Allow the poll to happen
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RegisterEventProcessorWithBadSupplierThatThrowsHasExpectedResult()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(100)));
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        var name = _fixture.Create<EventProcessorName>();
        var callCount = 0;
        var completion = new TaskCompletionSource();
        Func<Task<EventProcessorInfo?>> supplier = () =>
        {
            callCount++;
            switch (callCount)
            {
                case < 2:
                    throw new Exception();
                case 2:
                    completion.SetResult();
                    break;
            }

            return Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
            {
                Running = true,
                AvailableThreads = 1,
                ActiveThreads = 1,
                Error = false,
                IsStreamingProcessor = false,
                Mode = "Tracking",
                ProcessorName = name.ToString()
            });
        };
        
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompletedAsync();

        //Allow the poll to happen
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task RegisterEventProcessorWithBadSupplierThatNeverReturnsHasExpectedResult()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(100)));
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        var name = _fixture.Create<EventProcessorName>();
        var callCount = 0;
        var completion = new TaskCompletionSource();
        Func<Task<EventProcessorInfo?>> supplier = async () =>
        {
            callCount++;
            switch (callCount)
            {
                case 1:
                    await Task.Delay(Timeout.InfiniteTimeSpan);
                    break;
                case 2:
                    completion.SetResult();
                    break;
            }

            return new EventProcessorInfo
            {
                Running = true,
                AvailableThreads = 1,
                ActiveThreads = 1,
                Error = false,
                IsStreamingProcessor = false,
                Mode = "Tracking",
                ProcessorName = name.ToString()
            };
        };
        
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompletedAsync();

        //Allow the poll to happen
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task EventProcessorHandlesStartRequestFromAdminChannel()
    {
        var sut = await CreateSystemUnderTest();
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        var admin = sut.AdminChannel;
        
        var name = _fixture.Create<EventProcessorName>();
        Func<Task<EventProcessorInfo?>> supplier = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 1,
            Error = false,
            IsStreamingProcessor = false,
            Mode = "Tracking",
            ProcessorName = name.ToString()
        });

        var processor = new AwaitableEventProcessor();
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, processor);
        await registration.WaitUntilCompletedAsync();

        // Allow Axon Server to learn about this event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var result = await admin.StartEventProcessorAsync(name, TokenStoreIdentifier.Empty);
        Assert.Equal( Result.Success, result);
        
        await processor.StartCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task EventProcessorHandlesPauseRequestFromAdminChannel()
    {
        var sut = await CreateSystemUnderTest();
        await sut.WaitUntilReadyAsync();
        
        var control = sut.ControlChannel;
        var admin = sut.AdminChannel;
        
        var name = _fixture.Create<EventProcessorName>();
        
        Func<Task<EventProcessorInfo?>> supplier = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 1,
            Error = false,
            IsStreamingProcessor = false,
            Mode = "Tracking",
            ProcessorName = name.ToString()
        });

        var processor = new AwaitableEventProcessor();
        await using var registration = await control.RegisterEventProcessorAsync(name, supplier, processor);
        await registration.WaitUntilCompletedAsync();

        // Allow Axon Server to learn about this event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var result = await admin.PauseEventProcessorAsync(name, TokenStoreIdentifier.Empty);
        Assert.Equal( Result.Success, result);
        
        await processor.PauseCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task EventProcessorHandlesMoveSegmentRequestFromAdminChannel()
    {
        var fromClient = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(500)));
        var toClient = await CreateSystemUnderTest();
        await Task.WhenAll(fromClient.WaitUntilReadyAsync(), toClient.WaitUntilReadyAsync());
        
        var fromControl = fromClient.ControlChannel;

        var name = _fixture.Create<EventProcessorName>();
        var id = _fixture.Create<TokenStoreIdentifier>();
        Func<Task<EventProcessorInfo?>> supplier1 = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            SegmentStatus =
            {
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 0,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                },
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 1,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                }
            },
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 1,
            Error = false,
            IsStreamingProcessor = true,
            Mode = "Tracking",
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = id.ToString()
        });

        var processor1 = new AwaitableEventProcessor();
        await using var registration1 = await fromControl.RegisterEventProcessorAsync(name, supplier1, processor1);
        await registration1.WaitUntilCompletedAsync();
        
        Func<Task<EventProcessorInfo?>> supplier2 = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 1,
            Error = false,
            IsStreamingProcessor = true,
            Mode = "Tracking",
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = id.ToString()
        });

        var toControl = toClient.ControlChannel;
        
        var processor2 = new AwaitableEventProcessor();
        await using var registration2 = await toControl.RegisterEventProcessorAsync(name, supplier2, processor2);
        await registration2.WaitUntilCompletedAsync();

        // Allow Axon Server to learn about these event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var admin = fromClient.AdminChannel;
        
        var result = await admin.MoveEventProcessorSegmentAsync(name, id, new SegmentId(0), toClient.ClientIdentity.ClientInstanceId);
        Assert.Equal( Result.Success, result);
        
        await processor1.ReleaseSegmentCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task EventProcessorHandlesSplitSegmentRequestFromAdminChannel()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(500)));
        await sut.WaitUntilReadyAsync();
        
        var name = _fixture.Create<EventProcessorName>();
        var id = _fixture.Create<TokenStoreIdentifier>();
        Func<Task<EventProcessorInfo?>> supplier = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            SegmentStatus =
            {
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 0,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                },
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 1,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                }
            },
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 4,
            Error = false,
            IsStreamingProcessor = true,
            Mode = "Tracking",
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = id.ToString()
        });

        var processor = new AwaitableEventProcessor();
        await using var registration = await sut.ControlChannel.RegisterEventProcessorAsync(name, supplier, processor);
        await registration.WaitUntilCompletedAsync();
        
        // Allow Axon Server to learn about these event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var admin = sut.AdminChannel;
        
        var result = await admin.SplitEventProcessorAsync(name, id);
        Assert.Equal( Result.Success, result);
        
        await processor.SplitSegmentCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    [Fact]
    public async Task EventProcessorHandlesMergeSegmentRequestFromAdminChannel()
    {
        var sut = await CreateSystemUnderTest(
            options => options.WithEventProcessorUpdateFrequency(TimeSpan.FromMilliseconds(500)));
        await sut.WaitUntilReadyAsync();
        
        var name = _fixture.Create<EventProcessorName>();
        var id = _fixture.Create<TokenStoreIdentifier>();
        Func<Task<EventProcessorInfo?>> supplier = () => Task.FromResult<EventProcessorInfo?>(new EventProcessorInfo
        {
            SegmentStatus =
            {
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 0,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                },
                new EventProcessorInfo.Types.SegmentStatus
                {
                    SegmentId = 1,
                    OnePartOf = 2,
                    CaughtUp = false,
                    TokenPosition = Random.Shared.Next(1, 10000),
                    Replaying = true
                }
            },
            Running = true,
            AvailableThreads = 1,
            ActiveThreads = 4,
            Error = false,
            IsStreamingProcessor = true,
            Mode = "Tracking",
            ProcessorName = name.ToString(),
            TokenStoreIdentifier = id.ToString()
        });

        var processor = new AwaitableEventProcessor();
        await using var registration = await sut.ControlChannel.RegisterEventProcessorAsync(name, supplier, processor);
        await registration.WaitUntilCompletedAsync();
        
        // Allow Axon Server to learn about these event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var admin = sut.AdminChannel;
        
        var result = await admin.MergeEventProcessorAsync(name, id);
        Assert.Equal( Result.Success, result);
        
        await processor.MergeSegmentCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
    
    private class EmptyEventProcessor : IEventProcessorInstructionHandler
    {
        public Task<bool> ReleaseSegmentAsync(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SplitSegmentAsync(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task<bool> MergeSegmentAsync(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task PauseAsync()
        {
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }
    }
    
    private class AwaitableEventProcessor : IEventProcessorInstructionHandler
    {
        public TaskCompletionSource ReleaseSegmentCompletion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SplitSegmentCompletion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource MergeSegmentCompletion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PauseCompletion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StartCompletion { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        public Task<bool> ReleaseSegmentAsync(SegmentId segment)
        {
            ReleaseSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> SplitSegmentAsync(SegmentId segment)
        {
            SplitSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> MergeSegmentAsync(SegmentId segment)
        {
            MergeSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task PauseAsync()
        {
            PauseCompletion.TrySetResult();
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            StartCompletion.TrySetResult();
            return Task.CompletedTask;
        }
    }
}