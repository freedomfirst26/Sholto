using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>An event handler for a flow that publishes from another thread: a test awaits the first event
/// that arrives (with a timeout) instead of polling. Later events are ignored.</summary>
internal sealed class F9AwaitableEventHandler<T> : IEventHandler<T> where T : struct, IEvent
{
    private readonly TaskCompletionSource<T> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>True once an event has arrived.</summary>
    public bool HasEvent => _first.Task.IsCompleted;

    public void Handle(in T e) => _first.TrySetResult(e);

    public async Task<T> NextAsync()
    {
        var finished = await Task.WhenAny(_first.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (finished != _first.Task) throw new TimeoutException($"No {typeof(T).Name} was published.");
        return await _first.Task;
    }
}
