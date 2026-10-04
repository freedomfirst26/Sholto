using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Enums;

namespace Sholto.App.Audio;

public sealed class MiniAudioOutputEnumerator : IAudioOutputEnumerator
{
    /// <summary>Spins up its own short-lived <c>MiniAudioEngine</c> to enumerate
    /// playback devices, rather than sharing the one <see cref="AudioEngine"/> owns
    /// while streaming. This is deliberate, not a leftover: the composition root
    /// calls this BEFORE any <see cref="AudioEngine"/> exists (device enumeration
    /// picks which device to open), and calls it again later, from the "change
    /// output device" menu action, whether or not an engine happens to be running
    /// at that moment. Sharing would mean either constructing this from a nullable
    /// live engine (which doesn't exist at first call) or reaching into a running
    /// playback engine's device-info refresh from an unrelated menu action — more
    /// coupling than a rare (startup + occasional user action), cheap, short-lived
    /// enumeration is worth.</summary>
    public IReadOnlyList<AudioDevice> EnumerateOutputs()
    {
        using var engine = new MiniAudioEngine();
        engine.UpdateAudioDevicesInfo();
        return engine.PlaybackDevices
            .Select(d => new AudioDevice(d.Name, d.IsDefault))
            .ToList();
    }
}
