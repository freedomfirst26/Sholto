using Sholto.App.Audio;

namespace Sholto.App.Lifecycle;

/// <summary>The audio engine behind the two decks: created when the first output device is chosen,
/// switched to another device later, stopped at exit.</summary>
public interface IAudioOutput
{
    /// <summary>Create the engine over the two decks and start it on <paramref name="deviceName"/> (null: the
    /// controller's own sound card carries master and cue). A failure is logged, not thrown. Returns where
    /// master really plays (Silent when the engine failed to start).</summary>
    Task<MasterRoute> StartAsync(string? deviceName);

    /// <summary>Move master to <paramref name="deviceName"/> with the engine's runtime device switch
    /// (preserves the audio graph); when no engine exists yet, create and start it there. A failure is
    /// logged, not thrown. Returns where master really plays, as <see cref="StartAsync"/>.</summary>
    Task<MasterRoute> SwitchAsync(string deviceName);

    /// <summary>Stop the engine, if there is one.</summary>
    void Stop();
}
