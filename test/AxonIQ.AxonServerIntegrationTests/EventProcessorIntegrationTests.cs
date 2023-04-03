using AutoFixture;
using AxonIQ.AxonServer.Connector.IntegrationTests.Containerization;
using AxonIQ.AxonServer.Connector.Tests;
using AxonIQ.AxonServer.Connector.Tests.Framework;
using AxonIQ.AxonServer.Embedded;
using Io.Axoniq.Axonserver.Grpc.Control;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.IntegrationTests;

[Collection(nameof(AxonServerWithAccessControlDisabledCollection))]
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
        _loggerFactory = new TestOutputHelperLoggerFactory(output);
    }
    
    private Task<IAxonServerConnection> CreateSystemUnderTest(
        Action<IAxonServerConnectionFactoryOptionsBuilder>? configure = default)
    {
        var component = _fixture.Create<ComponentName>();
        var clientInstance = _fixture.Create<ClientInstanceId>();

        var builder = AxonServerConnectionFactoryOptions.For(component, clientInstance)
            .WithRoutingServers(_container.GetGrpcEndpoint())
            .WithLoggerFactory(_loggerFactory);
        configure?.Invoke(builder);
        var options = builder.Build();
        var factory = new AxonServerConnectionFactory(options);
        return factory.ConnectAsync(Context.Default);
    }

    [Fact]
    public async Task RegisterEventProcessorCausesPeriodicInfoPollToHappen()
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
        
        await using var registration = await control.RegisterEventProcessor(name, supplier, new EmptyEventProcessor());
        await registration.WaitUntilCompleted();

        //Allow the poll to happen
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
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
        await using var registration = await control.RegisterEventProcessor(name, supplier, processor);
        await registration.WaitUntilCompleted();

        // Allow Axon Server to learn about this event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var result = await admin.StartEventProcessor(name, TokenStoreIdentifier.Empty);
        Assert.Equal( Io.Axoniq.Axonserver.Grpc.Admin.Result.Success, result);
        
        await processor.StartCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
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
        await using var registration = await control.RegisterEventProcessor(name, supplier, processor);
        await registration.WaitUntilCompleted();

        // Allow Axon Server to learn about this event processor
        await Task.Delay(TimeSpan.FromSeconds(1));

        var result = await admin.PauseEventProcessor(name, TokenStoreIdentifier.Empty);
        Assert.Equal( Io.Axoniq.Axonserver.Grpc.Admin.Result.Success, result);
        
        await processor.PauseCompletion.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    }
    
    private class EmptyEventProcessor : IEventProcessorInstructionHandler
    {
        public Task<bool> ReleaseSegment(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SplitSegment(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task<bool> MergeSegment(SegmentId segment)
        {
            return Task.FromResult(true);
        }

        public Task Pause()
        {
            return Task.CompletedTask;
        }

        public Task Start()
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
        
        public Task<bool> ReleaseSegment(SegmentId segment)
        {
            ReleaseSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> SplitSegment(SegmentId segment)
        {
            SplitSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task<bool> MergeSegment(SegmentId segment)
        {
            MergeSegmentCompletion.TrySetResult();
            return Task.FromResult(true);
        }

        public Task Pause()
        {
            PauseCompletion.TrySetResult();
            return Task.CompletedTask;
        }

        public Task Start()
        {
            StartCompletion.TrySetResult();
            return Task.CompletedTask;
        }
    }
}