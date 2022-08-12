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

public class TestOutputHelperLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string? _categoryName;

    public TestOutputHelperLogger(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _categoryName = null;
    }
    
    public TestOutputHelperLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine(_categoryName != null
            ? $"[{logLevel.ToString()}]:{_categoryName}:{formatter(state, exception)}"
            : $"[{logLevel.ToString()}]:{formatter(state, exception)}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return NullDisposable.Instance;
    }
}