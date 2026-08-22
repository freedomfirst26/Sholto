using SoundFlow.Backends.MiniAudio;
using SoundFlow.Backends.MiniAudio.Enums;

namespace Sholto.Audio;

/// <summary>
/// User-facing handle to a playback device. The Name matches the OS-friendly
/// PulseAudio/PipeWire sink description shown in the system sound panel.
/// Only the name + default flag cross the boundary; the native handle is
/// re-resolved against a live engine when actually opening the device.
/// </summary>
public sealed record AudioDevice(string Name, bool IsDefault)
{
    public override string ToString() => IsDefault ? $"{Name} (default)" : Name;
}

public static class AudioDevices
{
    public static IReadOnlyList<AudioDevice> EnumerateOutputs()
    {
        using var engine = new MiniAudioEngine();
        engine.UpdateAudioDevicesInfo();
        return engine.PlaybackDevices
            .Select(d => new AudioDevice(d.Name, d.IsDefault))
            .ToList();
    }
}
