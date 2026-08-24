namespace Sholto.Audio;

/// <summary>A per-container decode strategy. Turns one audio file into interleaved
/// float PCM at <see cref="AudioFileDecoder.TargetSampleRate"/> /
/// <see cref="AudioFileDecoder.TargetChannels"/>. One implementation per format so
/// each uses the decoder that actually works on this platform.</summary>
public interface IAudioDecodeStrategy
{
    /// <summary>True if this strategy handles the given lowercase extension
    /// (including the dot, e.g. ".flac").</summary>
    bool CanDecode(string extension);

    /// <summary>Decode to 48 kHz stereo interleaved float PCM.</summary>
    float[] Decode(string filePath);
}
