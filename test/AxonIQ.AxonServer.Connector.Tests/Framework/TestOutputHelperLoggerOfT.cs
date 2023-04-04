using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Framework;

public class TestOutputHelperLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestOutputHelperLogger(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine(exception == null
            ? $"[{logLevel.ToString()}]:{typeof(T).FullName}:{formatter(state, exception)}"
            : $"[{logLevel.ToString()}]:{typeof(T).FullName}:{formatter(state, exception)}:{exception.ToString()}");
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