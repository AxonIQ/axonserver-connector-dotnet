namespace AxonIQ.AxonServer.Connector;

internal static class TaskToAxonActorExtensions
{
    public static Task TellToAsync<T, TMessage>(
        this Task<T> task,
        IAxonActor<TMessage> actor,
        Func<T, TMessage> success,
        Func<Exception, TMessage> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (success == null) throw new ArgumentNullException(nameof(success));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return TellToCore(task, actor, success, failure, ct);
    }
    
    public static Task TellToAsync<T, TMessage>(
        this Task<T> task,
        IAxonActor<TMessage> actor,
        Func<TaskResult<T>, TMessage> translate,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (translate == null) throw new ArgumentNullException(nameof(translate));

        return TellToCore(task, actor, 
            value => translate(new TaskResult<T>.Ok(value)), 
            exception => translate(new TaskResult<T>.Error(exception)), 
            ct);
    }
    
    private static async Task TellToCore<T, TMessage>(
        Task<T> task, 
        IAxonActor<TMessage> actor, 
        Func<T, TMessage> success, 
        Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            await actor.TellAsync(success(result), ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }
    
    public static Task TellToAsync<T, TMessage>(
        this Func<Task<T>> task,
        IAxonActor<TMessage> actor,
        Func<TaskResult<T>, TMessage> translate,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (translate == null) throw new ArgumentNullException(nameof(translate));

        return TellToCore(task, actor, 
            value => translate(new TaskResult<T>.Ok(value)), 
            exception => translate(new TaskResult<T>.Error(exception)), 
            ct);
    }

    private static async Task TellToCore<T, TMessage>(
        Func<Task<T>> task, 
        IAxonActor<TMessage> actor, 
        Func<T, TMessage> success, 
        Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            var result = await task().ConfigureAwait(false);
            await actor.TellAsync(success(result), ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }

    public static Task TellToAsync<T, TMessage>(
        this ValueTask<T> task,
        IAxonActor<TMessage> actor,
        Func<T, TMessage> success,
        Func<Exception, TMessage> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (success == null) throw new ArgumentNullException(nameof(success));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return TellToCore(task, actor, success, failure, ct);
    }

    private static async Task TellToCore<T, TMessage>(ValueTask<T> task, IAxonActor<TMessage> actor, Func<T, TMessage> success, Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            await actor.TellAsync(success(result), ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }

    public static Task TellToAsync<TMessage>(
        this Task task,
        IAxonActor<TMessage> actor,
        Func<TMessage> success,
        Func<Exception, TMessage> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (success == null) throw new ArgumentNullException(nameof(success));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return TellToCore(task, actor, success, failure, ct);
    }
    
    public static Task TellToAsync<TMessage>(
        this Task task,
        IAxonActor<TMessage> actor,
        Func<TaskResult, TMessage> translate,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (translate == null) throw new ArgumentNullException(nameof(translate));

        return TellToCore(
            task, 
            actor, 
            () => translate(new TaskResult.Ok()),
            exception => translate(new TaskResult.Error(exception)), ct);
    }

    private static async Task TellToCore<TMessage>(Task task, IAxonActor<TMessage> actor, Func<TMessage> success, Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            await task.ConfigureAwait(false);
            await actor.TellAsync(success(), ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }
    
    public static Task TellToAsync<TMessage>(
        this Task task,
        IAxonActor<TMessage> actor,
        Func<Exception, TMessage> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return TellToCore(task, actor, failure, ct);
    }
    
    private static async Task TellToCore<TMessage>(Task task, IAxonActor<TMessage> actor, Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }

    public static Task ScheduleToAsync<TMessage>(
        this Task task,
        IAxonActor<TMessage> actor,
        Func<Exception, (TMessage, TimeSpan)> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return ScheduleToCore(task, actor, failure, ct);
    }
    
    private static async Task ScheduleToCore<TMessage>(
        Task task, 
        IAxonActor<TMessage> actor, 
        Func<Exception, (TMessage, TimeSpan)> failure,
        CancellationToken ct)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var (message, due) = failure(exception);
            await actor.ScheduleAsync(message, due, ct).ConfigureAwait(false);
        }
    }

    public static Task TellToAsync<TMessage>(
        this ValueTask task,
        IAxonActor<TMessage> actor,
        Func<TMessage> success,
        Func<Exception, TMessage> failure,
        CancellationToken ct = default
    )
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (success == null) throw new ArgumentNullException(nameof(success));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        return TellToCore(task, actor, success, failure, ct);
    }

    private static async Task TellToCore<TMessage>(ValueTask task, IAxonActor<TMessage> actor, Func<TMessage> success, Func<Exception, TMessage> failure,
        CancellationToken ct)
    {
        try
        {
            await task.ConfigureAwait(false);
            await actor.TellAsync(success(), ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await actor.TellAsync(failure(exception), ct).ConfigureAwait(false);
        }
    }
}