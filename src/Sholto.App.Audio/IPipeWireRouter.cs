using System.Diagnostics;

namespace Sholto.App.Audio;

/// <summary>Port onto <see cref="PipeWireRouter"/>'s world-touching operations —
/// consumed by <see cref="AudioEngine"/>, which is composed with a real instance at
/// bootstrap.</summary>
public interface IPipeWireRouter
{
    /// <summary>True if <c>pw-link</c> is on PATH. Everything else here no-ops
    /// (returns null/false/empty) when this is false.</summary>
    bool IsAvailable();

    /// <summary>All playback sinks except the controller's own sound card
    /// (<paramref name="exclude"/>) — i.e. the master-speaker choices
    /// the user is allowed to pick from. Returns empty (never throws) if
    /// <c>pactl</c> isn't available.</summary>
    IReadOnlyList<(string Node, string Desc)> EnumerateSpeakerSinks(IControllerSoundCard exclude);

    /// <summary>The controller's PipeWire sink node name, or null if it isn't
    /// currently connected/enumerated by PipeWire.</summary>
    string? FindControllerSink(IControllerSoundCard card);

    /// <summary>Re-link Sholto's master FL/FR output ports from the controller to
    /// <paramref name="speakerNode"/>. RL/RR (cue) are left connected to the
    /// controller. Never throws; returns false + a log line on any failure.</summary>
    bool ApplyMasterRoute(string controllerNode, string speakerNode, out string log);

    /// <summary>Relink master FL/FR back onto the controller (undo <see cref="ApplyMasterRoute"/>).</summary>
    void ResetMasterRoute(string controllerNode, IControllerSoundCard card);
}
