namespace Sholto.App.Audio;

/// <summary>Names the low-level audio backend (PulseAudio, ALSA, WASAPI, …) the opened
/// playback device runs on. Ask only after the device is initialised: the answer describes
/// the device <see cref="AudioEngine"/> really opened, not a guess made at engine start.</summary>
public interface IAudioBackendProbe
{
    /// <summary>The backend's name, or null when it cannot be told.</summary>
    string? ActiveBackend();
}
