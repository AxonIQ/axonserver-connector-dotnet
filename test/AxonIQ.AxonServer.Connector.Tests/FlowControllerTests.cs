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

public class FlowControllerTests
{
    [Fact]
    public void IncrementReturnsExpectedResult()
    {
        var sut = new FlowController(new PermitCount(2));

        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
        
        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
    }
    
    [Fact]
    public void ResetHasExpectedResult()
    {
        var sut = new FlowController(new PermitCount(2));

        Assert.False(sut.Increment());
        sut.Reset();
        Assert.False(sut.Increment());
        Assert.True(sut.Increment());
    }
}