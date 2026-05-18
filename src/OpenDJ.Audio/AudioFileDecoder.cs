using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenDJ.Audio;

public static class AudioFileDecoder
{
    public const int TargetSampleRate = 44100;
    public const int TargetChannels = 2;

    public static float[] Decode(string filePath)
    {
        using var reader = new AudioFileReader(filePath);

        ISampleProvider provider = reader;

        if (reader.WaveFormat.Channels == 1)
            provider = new MonoToStereoSampleProvider(provider);

        if (reader.WaveFormat.SampleRate != TargetSampleRate)
        {
            var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(TargetSampleRate, TargetChannels);
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);
        }

        var samples = new List<float>(
            capacity: (int)(reader.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels) + 1);
        var chunk = new float[4096];
        int read;

        while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
            samples.AddRange(chunk.AsSpan(0, read));

        return [.. samples];
    }
}
