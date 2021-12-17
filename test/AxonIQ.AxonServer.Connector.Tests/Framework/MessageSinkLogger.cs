using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace AxonIQ.AxonServer.Connector.Tests.Framework;

public class MessageSinkLogger : ILogger
{
    private readonly IMessageSink _sink;

    public MessageSinkLogger(IMessageSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _sink.OnMessage(
            new DiagnosticMessage($"[{logLevel.ToString()}]:{formatter(state, exception)}"));
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