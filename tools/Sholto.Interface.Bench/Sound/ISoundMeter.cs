namespace Sholto.Interface.Bench.Sound;

/// <summary>Turns a WAV file into numbers via ffmpeg's analysis filters.</summary>
public interface ISoundMeter
{
    SoundMeasurement Measure(string wavPath, double silenceThresholdDb = -50, double silenceMinDuration = 0.3);

    SoundMeasurement ParseStderr(string wavPath, string stderr);
}
