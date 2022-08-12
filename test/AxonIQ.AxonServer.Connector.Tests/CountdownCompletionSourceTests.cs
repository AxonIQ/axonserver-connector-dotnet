/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Xunit;

namespace AxonIQ.AxonServer.Connector.Tests;

public class CountdownCompletionSourceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InitialCountCanNotBeZeroOrNegative(int initialCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CountdownCompletionSource(initialCount));
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
            var randomSignalFaultAtIndex = Random.Shared.Next(0, _sut.InitialCount);
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