namespace Sholto.Data;

/// <summary>The in-process bus: commands and queries in, events out. Everything runs synchronously on
/// the caller's thread; the App assumes a single thread (see <see cref="IAppThread"/>) and takes no
/// locks. Lookups are per-instance dictionaries keyed by message type, cast to the typed handler, so
/// struct messages are never boxed and Send/Publish allocate nothing after warm-up.
/// <para>Failures (handler throws, no handler, runaway re-entrancy) go to
/// <see cref="IHandlerFailureSink"/>; they are never rethrown.</para></summary>
public sealed class DataBus(IHandlerFailureSink failures)
    : ICommandSender, IQueryAsker, IEventSubscriber, ICommandRegistry, IQueryRegistry, IEventPublisher
{
    /// <summary>Publishing from inside a handler nests; deeper than this is treated as a loop and dropped.</summary>
    public const int MaxPublishDepth = 16;

    private readonly IHandlerFailureSink _failures = failures;
    private readonly Dictionary<Type, object> _commands = [];
    private readonly Dictionary<Type, object> _queries = [];
    private readonly Dictionary<Type, object> _channels = [];
    private int _publishDepth;

    public void Register<T>(ICommandHandler<T> handler) where T : struct, ICommand
    {
        if (!_commands.TryAdd(typeof(T), handler))
            throw new InvalidOperationException($"A handler for command {typeof(T).Name} is already registered.");
    }

    public void Register<TQ, TR>(IQueryHandler<TQ, TR> handler) where TQ : struct, IQuery<TR>
    {
        if (!_queries.TryAdd(typeof(TQ), handler))
            throw new InvalidOperationException($"A handler for query {typeof(TQ).Name} is already registered.");
    }

    public void Send<T>(in T command) where T : struct, ICommand
    {
        if (!_commands.TryGetValue(typeof(T), out var h))
        {
            Report(typeof(T), null, new InvalidOperationException($"No handler registered for command {typeof(T).Name}."));
            return;
        }
        try { ((ICommandHandler<T>)h).Handle(in command); }
        catch (Exception ex) { Report(typeof(T), h, ex); }
    }

    public TR Ask<TQ, TR>(in TQ query) where TQ : struct, IQuery<TR>
    {
        if (!_queries.TryGetValue(typeof(TQ), out var h))
        {
            Report(typeof(TQ), null, new InvalidOperationException($"No handler registered for query {typeof(TQ).Name}."));
            return default!;
        }
        try { return ((IQueryHandler<TQ, TR>)h).Handle(in query); }
        catch (Exception ex) { Report(typeof(TQ), h, ex); return default!; }
    }

    public void Publish<T>(in T e) where T : struct, IEvent
    {
        if (_publishDepth >= MaxPublishDepth)
        {
            Report(typeof(T), null, new InvalidOperationException(
                $"Publish of {typeof(T).Name} nested deeper than {MaxPublishDepth}; dropped."));
            return;
        }
        var channel = ChannelFor<T>();
        channel.Record(in e);
        _publishDepth++;
        try
        {
            // The array is replaced, never mutated, so a handler may (un)subscribe mid-publish.
            foreach (var handler in channel.Handlers)
            {
                try { handler.Handle(in e); }
                catch (Exception ex) { Report(typeof(T), handler, ex); }
            }
        }
        finally { _publishDepth--; }
    }

    public IDisposable Subscribe<T>(IEventHandler<T> handler) where T : struct, IEvent
    {
        var channel = ChannelFor<T>();
        // Replay current state first, then join the live list, so the handler never sees
        // an old value after a newer one.
        foreach (var state in channel.Snapshot())
        {
            try { handler.Handle(in state); }
            catch (Exception ex) { Report(typeof(T), handler, ex); }
        }
        channel.Handlers = [.. channel.Handlers, handler];
        return new Subscription<T>(channel, handler);
    }

    private Channel<T> ChannelFor<T>() where T : struct, IEvent
    {
        if (_channels.TryGetValue(typeof(T), out var existing)) return (Channel<T>)existing;
        // StateChannel<T> carries a stricter constraint (IStateEvent), so it is closed by reflection once per type.
        var created = typeof(IStateEvent).IsAssignableFrom(typeof(T))
            ? (Channel<T>)Activator.CreateInstance(typeof(StateChannel<>).MakeGenericType(typeof(T)))!
            : new Channel<T>();
        _channels[typeof(T)] = created;
        return created;
    }

    private void Report(Type messageType, object? handler, Exception ex)
    {
        try { _failures.Report(new HandlerFailure(messageType, handler, ex)); }
        catch { /* a failing sink must not break the bus */ }
    }
}
