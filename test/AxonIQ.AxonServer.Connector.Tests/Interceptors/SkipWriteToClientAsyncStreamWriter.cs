using Grpc.Core;

namespace AxonIQ.AxonServer.Connector.Tests.Interceptors;

internal class SkipWriteToClientAsyncStreamWriter<TRequest> : IClientStreamWriter<TRequest>
{
    private readonly IClientStreamWriter<TRequest> _writer;
    private readonly Predicate<object?> _skip;

    public SkipWriteToClientAsyncStreamWriter(IClientStreamWriter<TRequest> writer, Predicate<object?> skip)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _skip = skip ?? throw new ArgumentNullException(nameof(skip));
    }
    public Task WriteAsync(TRequest message)
    {
        return _skip(message) ? Task.CompletedTask : _writer.WriteAsync(message);
    }

    public WriteOptions? WriteOptions
    {
        get => _writer.WriteOptions;
        set => _writer.WriteOptions = value;
    }
    
    public Task CompleteAsync()
    {
        return _writer.CompleteAsync();
    }
}