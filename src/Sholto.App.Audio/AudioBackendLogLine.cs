namespace Sholto.App.Audio;

/// <summary>Writes <see cref="AudioEngine"/>'s "device started" log line. The backend is asked
/// of <paramref name="probe"/> when the line is written — after the device is initialised — and
/// is "unknown" when the probe cannot tell.</summary>
public sealed class AudioBackendLogLine(IAudioBackendProbe probe)
{
    private const string Unknown = "unknown";

    private readonly IAudioBackendProbe _probe = probe;

    public string DeviceStarted(string? deviceName, int reportedChannels, int openedChannels, uint periodMs, uint periods)
    {
        string backend = _probe.ActiveBackend() ?? Unknown;
        string mode = openedChannels >= 4 ? "master 1-2 + headphone cue 3-4" : "stereo master (no cue bus)";
        string reported = reportedChannels > 0 ? $"{reportedChannels}ch" : "none";
        return $"backend={backend}; device={deviceName} started; reported {reported}, opened {openedChannels}ch — {mode}; buffer={periodMs}ms × {periods}";
    }
}
