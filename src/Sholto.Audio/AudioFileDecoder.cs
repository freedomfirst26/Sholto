using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace Sholto.Audio;

public static class AudioFileDecoder
{
    // Match AudioEngine output rate so SoundFlow doesn't have to resample on
     // playback — a rate mismatch here makes the audio play at engineRate/sourceRate
     // speed (e.g. 48000/44100 = 8.8% too fast).
    public const int TargetSampleRate = 48000;
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

            // Allocate the final array directly — no List<float> + spread copy. On a
            // 4-minute stereo track this avoids a ~90 MB realloc + memcpy at the end.
            // TotalTime is an estimate (sometimes off by a frame or two on VBR MP3),
            // so we add a small safety pad and trim if Read stops short.
            long estimatedSamples = (long)(waveStream.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels)
                                    + TargetSampleRate * TargetChannels; // +1 sec pad
            var samples = new float[estimatedSamples];
            int filled = 0;
            int read;
            while ((read = provider.Read(samples, filled, samples.Length - filled)) > 0)
            {
                filled += read;
                if (filled == samples.Length)
                {
                    // Decoder produced more than TotalTime advertised — grow geometrically.
                    Array.Resize(ref samples, samples.Length + samples.Length / 2);
                }
            }

            if (filled != samples.Length) Array.Resize(ref samples, filled);
            return samples;
        }
    }
}
