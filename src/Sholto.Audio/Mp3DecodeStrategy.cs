using NAudio.Wave;
using NLayer.NAudioSupport;

namespace Sholto.Audio;

/// <summary>MP3 via NLayer's fully-managed decoder. NAudio's own MP3 reader uses
/// MediaFoundation (mfplat.dll), which isn't present on Linux — NLayer sidesteps
/// that. Output is normalised through the shared NAudio resample tail.</summary>
public sealed class Mp3DecodeStrategy : IAudioDecodeStrategy
{
    public bool CanDecode(string extension) => extension == ".mp3";

    public float[] Decode(string filePath) =>
        AudioFileDecoder.ReadNAudioToFloats(
            new Mp3FileReaderBase(filePath, fmt => new Mp3FrameDecompressor(fmt)));
}
