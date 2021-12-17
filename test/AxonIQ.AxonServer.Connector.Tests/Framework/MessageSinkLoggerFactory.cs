using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AxonIQ.AxonServer.Connector.Tests.Framework;

public class MessageSinkLoggerFactory : ILoggerFactory
{
    private readonly IMessageSink _sink;

    public MessageSinkLoggerFactory(IMessageSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (categoryName == null) throw new ArgumentNullException(nameof(categoryName));
        return new MessageSinkLogger(_sink);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
    }
    
    public void Dispose()
    {
    }
}