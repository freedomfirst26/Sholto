namespace Sholto.App.Audio.Recording;

/// <summary>Tunables for recording the mix to a WAV file.</summary>
public sealed class MixRecordingOptions
{
    /// <summary>The folder recordings go in; empty means the OS Music folder.</summary>
    public string Folder { get; set; } = "";

    /// <summary>Seconds of audio the capture ring holds before the audio thread starts dropping frames.</summary>
    public int RingSeconds { get; set; } = 10;

    /// <summary>How long the writer thread sleeps when the ring is empty, in milliseconds.</summary>
    public int DrainIntervalMs { get; set; } = 50;

    /// <summary>Seconds of written audio between WAV header rewrites, so a crash leaves a playable file.</summary>
    public int FlushIntervalSeconds { get; set; } = 5;

    /// <summary>How long stopping waits for the writer thread to drain and finish, in milliseconds.</summary>
    public int StopTimeoutMs { get; set; } = 3000;
}
