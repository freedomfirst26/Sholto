using Sholto.App;
using Sholto.App.Audio;
using Sholto.Interface.Bench.Controller;

namespace Sholto.Interface.Bench.Headless;

/// <summary>One mounted headless run: the core, its two concrete decks, the controller-side pipeline
/// and the driver that handles the scenario's gesture/midi/scan steps. The session owns its decks:
/// <see cref="Dispose"/> unloads both, closing each loaded track's streaming source file.</summary>
public sealed record HeadlessSession(CoreStack Core, Deck Deck1, Deck Deck2, GestureHost Gestures, HeadlessScenarioDriver Driver) : IDisposable
{
    private bool _disposed;

    /// <summary>Unloads both decks. A second call does nothing.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Deck1.Unload(); }
        finally { Deck2.Unload(); }
    }
}
