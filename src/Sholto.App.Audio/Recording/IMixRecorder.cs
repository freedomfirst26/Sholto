namespace Sholto.App.Audio.Recording;

/// <summary>The master tap's switch: armed with a ring it copies the master into it, disarmed it ignores the audio.
/// One for the app's life, so an output-device switch that rebuilds the router keeps recording.</summary>
public interface IMixRecorder
{
    /// <summary>Zeroes both counts, then starts copying each master buffer into <paramref name="ring"/>.</summary>
    void Arm(IStereoSampleWriter ring);

    /// <summary>Stops copying. A buffer already being copied on the audio thread may still land.</summary>
    void Disarm();

    /// <summary>Frames copied into the ring since the last <see cref="Arm"/>.</summary>
    long CapturedFrames { get; }

    /// <summary>Frames dropped since the last <see cref="Arm"/> because the ring was full.</summary>
    long DroppedFrames { get; }
}
