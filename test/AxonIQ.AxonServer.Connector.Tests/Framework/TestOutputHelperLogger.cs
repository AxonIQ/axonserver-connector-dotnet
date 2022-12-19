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
        if (exception == null)
        {
            _output.WriteLine(_categoryName != null
                ? $"[{logLevel.ToString()}]:{_categoryName}:{formatter(state, exception)}"
                : $"[{logLevel.ToString()}]:{formatter(state, exception)}");
        }
        else
        {
            _output.WriteLine(_categoryName != null
                ? $"[{logLevel.ToString()}]:{_categoryName}:{formatter(state, exception)}:{exception.ToString()}"
                : $"[{logLevel.ToString()}]:{formatter(state, exception)}:{exception.ToString()}");
        }
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