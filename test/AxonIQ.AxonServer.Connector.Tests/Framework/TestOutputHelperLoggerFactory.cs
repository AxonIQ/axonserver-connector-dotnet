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

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Framework;

public class TestOutputHelperLoggerFactory : ILoggerFactory
{
    private readonly ITestOutputHelper _output;

    public TestOutputHelperLoggerFactory(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (categoryName == null) throw new ArgumentNullException(nameof(categoryName));
        return new TestOutputHelperLogger(_output, categoryName);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
    }
    
    public void Dispose()
    {
    }
}