using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CountdownCompletionSourceTests
{
    [Fact]
    public void InitialCountCanNotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CountdownCompletionSource(-1));
    }

    public class WhenInitialCountIsZero
    {
        private readonly CountdownCompletionSource _sut;

        public WhenInitialCountIsZero()
        {
            _sut = new CountdownCompletionSource(0);
        }
        
        [Fact]
        public void CompletesImmediately()
        {
            Assert.Same(Task.CompletedTask, _sut.Completion);
        }

        [Fact]
        public void InitialCountReturnsExpectedValue()
        {
            Assert.Equal(0, _sut.InitialCount);
        }
        
        [Fact]
        public void CurrentCountReturnsExpectedValue()
        {
            Assert.Equal(0, _sut.InitialCount);
        }

        [Fact]
        public void SignalFaultReturnsExpectedResult()
        {
            Assert.False(_sut.SignalFault(new Exception()));
            Assert.Equal(0, _sut.CurrentCount);
        }
        
        [Fact]
        public void SignalSuccessReturnsExpectedResult()
        {
            Assert.False(_sut.SignalSuccess());
            Assert.Equal(0, _sut.CurrentCount);
        }
    }
    
    public class WhenInitialCountIsOne
    {
        private readonly CountdownCompletionSource _sut;

        public WhenInitialCountIsOne()
        {
            _sut = new CountdownCompletionSource(1);
        }
        
        [Fact]
        public void DoesNotCompleteImmediately()
        {
            Assert.False(_sut.Completion.IsCompleted);
        }

        [Fact]
        public void SignalFaultReturnsExpectedResult()
        {
            Assert.True(_sut.SignalFault(new Exception()));
            Assert.True(_sut.Completion.IsCompleted);
            Assert.True(_sut.Completion.IsFaulted);
            Assert.Equal(1, _sut.CurrentCount);
        }
        
        [Fact]
        public void SignalSuccessReturnsExpectedResult()
        {
            Assert.True(_sut.SignalSuccess());
            Assert.True(_sut.Completion.IsCompleted);
            Assert.False(_sut.Completion.IsFaulted);
            Assert.True(_sut.Completion.IsCompletedSuccessfully);
            Assert.Equal(1, _sut.CurrentCount);
        }
    }
    
    public class WhenInitialCountIsGreaterThanOne
    {
        private readonly CountdownCompletionSource _sut;

        public WhenInitialCountIsGreaterThanOne()
        {
            _sut = new CountdownCompletionSource(Random.Shared.Next(2, 5));
        }
        
        [Fact]
        public void DoesNotCompleteImmediately()
        {
            Assert.False(_sut.Completion.IsCompleted);
        }

        [Fact]
        public void SignalFaultReturnsExpectedResult()
        {
            Assert.False(_sut.SignalFault(new Exception()));
            Assert.False(_sut.Completion.IsCompleted);
            Assert.Equal(1, _sut.CurrentCount);
        }
        
        [Fact]
        public void SignalSuccessReturnsExpectedResult()
        {
            Assert.False(_sut.SignalSuccess());
            Assert.False(_sut.Completion.IsCompleted);
            Assert.Equal(1, _sut.CurrentCount);
        }
        
        [Fact]
        public void ExactlyOneSignalFaultReturnsExpectedFinalResult()
        {
            var exception = new Exception();
            var randomSignalFaultAtIndex = Random.Shared.Next(2, _sut.InitialCount);
            for (var index = 0; index < _sut.InitialCount; index++)
            {
                if (index == randomSignalFaultAtIndex)
                {
                    if (index == _sut.InitialCount - 1)
                    {
                        Assert.True(_sut.SignalFault(exception));    
                    }
                    else
                    {
                        Assert.False(_sut.SignalFault(exception));
                    }
                }
                else
                {
                    if (index == _sut.InitialCount - 1)
                    {
                        Assert.True(_sut.SignalSuccess());
                    }
                    else
                    {
                        Assert.False(_sut.SignalSuccess());
                    }
                }
            }
            
            Assert.True(_sut.Completion.IsCompleted);
            Assert.True(_sut.Completion.IsFaulted);
            Assert.NotNull(_sut.Completion.Exception);
            Assert.Same(exception, _sut.Completion.Exception!.Flatten().InnerException);
        }
        
        [Fact]
        public void AllSignalFaultsReturnsExpectedFinalResult()
        {
            var exception = new Exception();
            for (var index = 0; index < _sut.InitialCount; index++)
            {
                if (index == _sut.InitialCount - 1)
                {
                    Assert.True(_sut.SignalFault(exception));
                }
                else
                {
                    Assert.False(_sut.SignalFault(exception));
                }
            }
            
            Assert.True(_sut.Completion.IsCompleted);
            Assert.True(_sut.Completion.IsFaulted);
            Assert.NotNull(_sut.Completion.Exception);
            Assert.Equal(
                Enumerable.Range(1, _sut.InitialCount).Select(_ => exception).ToArray()
                , _sut.Completion.Exception!.Flatten().InnerExceptions);
        }
        
        [Fact]
        public void AllSignalSuccessesReturnsExpectedFinalResult()
        {
            for (var index = 0; index < _sut.InitialCount; index++)
            {
                if (index == _sut.InitialCount - 1)
                {
                    Assert.True(_sut.SignalSuccess());
                }
                else
                {
                    Assert.False(_sut.SignalSuccess());
                }
            }
            
            Assert.True(_sut.Completion.IsCompleted);
            Assert.True(_sut.Completion.IsCompletedSuccessfully);
            Assert.Null(_sut.Completion.Exception);
        }
    }
}