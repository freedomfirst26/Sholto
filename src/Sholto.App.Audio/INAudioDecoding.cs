using NAudio.Wave;

namespace Sholto.App.Audio;

/// <summary>Shared NAudio tail for the MP3 and WAV/AIFF decode strategies: downmix,
/// resample to the target rate and read the whole stream into one float array.</summary>
public interface INAudioDecoding
{
    /// <summary>Reads <paramref name="waveStream"/> to interleaved stereo floats at the
    /// target sample rate. Takes ownership of (disposes) the stream.</summary>
    float[] ReadNAudioToFloats(WaveStream waveStream);
}
