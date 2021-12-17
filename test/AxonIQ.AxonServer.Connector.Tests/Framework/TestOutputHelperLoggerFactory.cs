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
        return new TestOutputHelperLogger(_output);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
    }
    
    public void Dispose()
    {
    }
}