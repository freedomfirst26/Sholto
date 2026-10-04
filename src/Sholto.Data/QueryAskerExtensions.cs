namespace Sholto.Data;

/// <summary>Asking a query whose answer is a <see cref="Task{TResult}"/>. Extension methods, so
/// <see cref="IQueryAsker"/> stays the one member the bus defines.</summary>
public static class QueryAskerExtensions
{
    /// <summary>Ask a query answered by a task. If the bus reports a failure (no handler, handler threw) the
    /// result is an already-completed task holding <c>default</c>, never a null.</summary>
    public static Task<TR> AskAsync<TQ, TR>(this IQueryAsker asker, in TQ query) where TQ : struct, IQuery<Task<TR>> =>
        asker.Ask<TQ, Task<TR>>(in query) ?? Task.FromResult<TR>(default!);
}
