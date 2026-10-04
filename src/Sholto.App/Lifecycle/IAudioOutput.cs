namespace Sholto.App.Lifecycle;

/// <summary>The audio engine behind the two decks: created when the first output device is chosen,
/// switched to another device later, stopped at exit.</summary>
public interface IAudioOutput
{
    /// <summary>Create the engine over the two decks and start it on <paramref name="deviceName"/> (null: the
    /// controller's own sound card carries master and cue). A failure is logged, not thrown.</summary>
    Task StartAsync(string? deviceName);

    /// <summary>Move master to <paramref name="deviceName"/> with the engine's runtime device switch
    /// (preserves the audio graph); when no engine exists yet, create and start it there. A failure is
    /// logged, not thrown.</summary>
    Task SwitchAsync(string deviceName);

    /// <summary>Stop the engine, if there is one.</summary>
    void Stop();
}
