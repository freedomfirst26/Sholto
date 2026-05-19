using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace Sholto.Audio;

public static class AudioFileDecoder
{
    public const int TargetSampleRate = 44100;
    public const int TargetChannels = 2;

    public static float[] Decode(string filePath)
    {
        // NAudio.AudioFileReader uses MediaFoundation on Linux which fails (no mfplat.dll).
        // Use NLayer for MP3 explicitly, NAudio for WAV/AIFF.
        WaveStream waveStream = filePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
            ? new Mp3FileReaderBase(filePath, fmt => new Mp3FrameDecompressor(fmt))
            : new AudioFileReader(filePath);

        using (waveStream)
        {
            ISampleProvider provider = waveStream is ISampleProvider sp
                ? sp
                : waveStream.ToSampleProvider();

            if (provider.WaveFormat.Channels == 1)
                provider = new MonoToStereoSampleProvider(provider);

            if (provider.WaveFormat.SampleRate != TargetSampleRate)
                provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);

            var samples = new List<float>(
                capacity: (int)(waveStream.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels) + 1);
            var chunk = new float[4096];
            int read;
            while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
                samples.AddRange(chunk.AsSpan(0, read));

            return [.. samples];
        }
    }
}
