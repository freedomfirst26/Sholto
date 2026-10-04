using Sholto.Data;

namespace Sholto.Interface.Controller.Tests;

/// <summary>An app thread that holds posted work until the test runs it, so the MIDI-thread hop is visible.</summary>
internal sealed class QueuedAppThread : IAppThread
{
    private readonly Queue<Action> _posted = new();

    public int PostedCount => _posted.Count;
    public bool IsCurrent => true;

    public void Post(Action action) => _posted.Enqueue(action);

    public void RunPosted()
    {
        while (_posted.Count > 0) _posted.Dequeue()();
    }
}
