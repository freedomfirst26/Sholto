namespace Sholto.Controller;

/// <summary>What <see cref="IControlSurface.Reset"/> is about to clear on the cue
/// buttons — every bit of state a physical press toggles rather than a value the
/// app streams to the LED. Take this before calling Reset, hand it to
/// <see cref="IControlSurface.RestoreCueState"/> afterwards.</summary>
public readonly record struct CueSnapshot(bool Deck1, bool Deck2, bool Master);

/// <summary>Port onto "wherever ControllerEvents come from" — the App composes
/// exactly one of these at bootstrap and talks to it in device-neutral language
/// only: it never touches MIDI, note numbers, or ALSA. <see cref="Controller"/> is
/// the real implementation (owns MIDI I/O to the DDJ-FLX4); <c>Sholto.Bench</c>'s
/// <c>ScriptedControlSurface</c> is the other one, replaying a scenario's events
/// instead of reading hardware — same downstream pipeline
/// (<c>GestureRecognizer</c> → <c>GestureBus</c> → <c>Orchestrator</c>) either way.
///
/// <para>The surface is one cohesive device role, not a grab-bag: connection
/// lifecycle, the semantic event stream, and the LED/light feedback that only
/// makes sense once you can identify which physical control sent the event that
/// caused it. A scripted surface has no LEDs to drive, but still has to answer to
/// the shape — its implementations are simply no-ops for the output side.</para>
/// </summary>
public interface IControlSurface : IDisposable
{
    /// <summary>High-level semantic events for the App.</summary>
    event Action<ControllerEvent>? Action;

    /// <summary>Raised (true) when the surface (re)connects, (false) when it drops
    /// out. Lets the App show a connection indicator. May fire on a background
    /// thread (the real implementation does) — marshal before touching UI.</summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>True while a controller is currently connected.</summary>
    bool IsConnected { get; }

    /// <summary>Connect. Returns false if no controller is found (the App can
    /// still run from the UI).</summary>
    bool Connect();

    /// <summary>Return the whole surface to a known state: every component reset
    /// (all button LEDs off) and the cleared cue state percolated up to the App so
    /// its cue audio clears too. Deliberately destructive — see
    /// <see cref="Controller.Reset"/> for the full contract callers rely on.</summary>
    void Reset();

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's BEAT SYNC LED.</summary>
    void SetBeatSync(int deck, bool on);

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's stem-mute pad LED (0=Drums, 1=Vocals, 2=Instrumental).</summary>
    void SetPadLight(int deck, int group, bool on);

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's echo-toggle pad LED.</summary>
    void SetEchoLight(int deck, bool on);

    /// <summary>Repaint the pad-mode (HOT CUE / PAD FX1) LEDs from whatever page
    /// each deck is actually on — see <see cref="Controller.ReassertPadPages"/>.</summary>
    void ReassertPadPages();

    /// <summary>Read the cue buttons' current on/off state — see
    /// <see cref="Controller.SnapshotCueState"/>.</summary>
    CueSnapshot SnapshotCueState();

    /// <summary>Undo what <see cref="Reset"/> did to the cue buttons — see
    /// <see cref="Controller.RestoreCueState"/>.</summary>
    void RestoreCueState(CueSnapshot snapshot);
}
