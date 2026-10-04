namespace Sholto.App.Audio;

/// <summary>Tempo-derived arithmetic shared by <see cref="EchoEffect"/> and
/// <see cref="BeatRepeatEffect"/>. Control-thread only: never called from
/// <c>Process</c> or <c>ProcessSample</c>.</summary>
public interface ITempoMath
{
    /// <summary>Beat count at a tempo to a sample count, clamped to
    /// <c>[1, capacityFrames - 1]</c>.</summary>
    int BeatsToSamples(double beats, double bpm, int sampleRate, int capacityFrames);

    /// <summary>One-pole ramp coefficient for a ~5 ms gain smoothing.</summary>
    float FiveMsGainAlpha(int sampleRate);
}
