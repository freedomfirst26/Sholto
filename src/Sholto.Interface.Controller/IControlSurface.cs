using Sholto.Data;

namespace Sholto.Interface.Controller;

/// <summary>Port onto "wherever ControllerEvents come from" — the App composes
/// exactly one of these at bootstrap and talks to it in device-neutral language
/// only: it never touches MIDI, note numbers, or ALSA. <see cref="Controller"/> is
/// the real implementation (owns MIDI I/O to the DDJ-FLX4); <c>Sholto.Interface.Bench</c>'s
/// <c>ScriptedControlSurface</c> is the other one, replaying a scenario's events
/// instead of reading hardware — same downstream pipeline
/// (<c>GestureRecognizer</c> → command bus → <c>Orchestrator</c>) either way.
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

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's BEAT SYNC LED.</summary>
    void SetBeatSync(int deck, bool on);

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's stem-mute pad LED (0=Drums, 1=Vocals, 2=Instrumental).</summary>
    void SetPadLight(int deck, int group, bool on);

    /// <summary>Output command from the App (via the orchestrator): drive a
    /// deck's echo-toggle pad LED.</summary>
    void SetEchoLight(int deck, bool on);

    /// <summary>Output command from the App: drive a deck's headphone-CUE LED.</summary>
    void SetHeadphoneCueLight(int deck, bool on);

    /// <summary>Output command from the App: drive the MASTER CUE LED.</summary>
    void SetMasterCueLight(bool on);

    /// <summary>Make a deck's pad page the App's: light the pad-mode buttons and, for a device that
    /// holds its pad mode itself, force it into that page.</summary>
    void SetPadPage(int deck, PadPage page);
}
