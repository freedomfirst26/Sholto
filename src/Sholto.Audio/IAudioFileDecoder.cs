namespace Sholto.Audio;

/// <summary>
/// Decodes an audio file into interleaved float PCM at <see cref="AudioFileDecoder.TargetSampleRate"/> /
/// <see cref="AudioFileDecoder.TargetChannels"/>. Extracted so collaborators that only
/// need to decode a file (e.g. <see cref="Deck"/>) can take this instead of the
/// concrete <see cref="AudioFileDecoder"/> — a test harness can then substitute a fake
/// decoder without touching a file on disk.
/// </summary>
public interface IAudioFileDecoder
{
    float[] Decode(string filePath);
}
