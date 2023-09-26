namespace AxonIQ.AxonServer.Connector;

internal static class AxonActorExtensions
{
    public static ValueTask TellToAsync<T, TMessage>(
        this IAxonActor<TMessage> actor,
        Func<T> function,
        Func<TaskResult<T>, TMessage> translate,
        CancellationToken ct = default)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (translate == null) throw new ArgumentNullException(nameof(translate));

        return TellToAsyncCore(actor, function, translate, ct);
    }

    private static async ValueTask TellToAsyncCore<T, TMessage>(
        IAxonActor<TMessage> actor,
        Func<T> function, Func<TaskResult<T>, TMessage> translate,
        CancellationToken ct)
    {
        try
        {
            var value = function();
            await actor.TellAsync(translate(new TaskResult<T>.Ok(value)), ct);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(translate(new TaskResult<T>.Error(exception)), ct);
        }
    }
}