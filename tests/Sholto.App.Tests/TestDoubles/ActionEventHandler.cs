using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>An event subscriber that runs the action it was given: an interface answering the App's
/// question from inside the event.</summary>
internal sealed class ActionEventHandler<T>(Action<T> action) : IEventHandler<T> where T : struct, IEvent
{
    private readonly Action<T> _action = action;

    public void Handle(in T e) => _action(e);
}
