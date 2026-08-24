using NAudio.Wave;

namespace Sholto.Audio;

/// <summary>WAV and AIFF via NAudio's managed readers (WaveFileReader / AiffFileReader,
/// selected by <see cref="AudioFileReader"/> on extension) — both work on Linux without
/// MediaFoundation. Output is normalised through the shared NAudio resample tail.</summary>
public sealed class WavDecodeStrategy : IAudioDecodeStrategy
{
    public bool CanDecode(string extension) =>
        extension is ".wav" or ".aiff" or ".aif";

    public float[] Decode(string filePath) =>
        AudioFileDecoder.ReadNAudioToFloats(new AudioFileReader(filePath));
}
