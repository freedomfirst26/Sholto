using Sholto.Data;

namespace Sholto.App;

/// <summary>Awaitable hops onto the app thread, for headless code that runs part of an async flow off it
/// (a scan, the startup sequence) and needs the result of a step that must run on it. Extension methods,
/// so <see cref="IAppThread"/> itself stays the two members the bus defines.</summary>
public static class AppThreadExtensions
{
    /// <summary>Run <paramref name="action"/> on the app thread and complete when it has run. Runs it at
    /// once when the caller already is the app thread. An exception it throws faults the returned task.</summary>
    public static Task InvokeAsync(this IAppThread appThread, Action action)
    {
        if (appThread.IsCurrent)
        {
            try { action(); return Task.CompletedTask; }
            catch (Exception ex) { return Task.FromException(ex); }
        }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        appThread.Post(() =>
        {
            try { action(); done.SetResult(); }
            catch (Exception ex) { done.SetException(ex); }
        });
        return done.Task;
    }

    /// <summary>As <see cref="InvokeAsync(IAppThread, Action)"/>, returning what <paramref name="func"/> returned.</summary>
    public static Task<T> InvokeAsync<T>(this IAppThread appThread, Func<T> func)
    {
        if (appThread.IsCurrent)
        {
            try { return Task.FromResult(func()); }
            catch (Exception ex) { return Task.FromException<T>(ex); }
        }
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        appThread.Post(() =>
        {
            try { done.SetResult(func()); }
            catch (Exception ex) { done.SetException(ex); }
        });
        return done.Task;
    }
}
