using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Sholto.Audio;

/// <summary>Shared tail for the NAudio-based strategies (MP3, WAV/AIFF): downmix
/// mono→stereo, resample to <see cref="AudioFileDecoder.TargetSampleRate"/>, and read
/// the whole stream into one float array. TotalTime is only an estimate on VBR sources,
/// so the buffer is padded and grown if the decoder overruns, then trimmed.</summary>
internal static class NAudioDecoding
{
    internal static float[] ReadNAudioToFloats(WaveStream waveStream)
    {
        using (waveStream)
        {
            ISampleProvider provider = waveStream is ISampleProvider sp
                ? sp
                : waveStream.ToSampleProvider();

            if (provider.WaveFormat.Channels == 1)
                provider = new MonoToStereoSampleProvider(provider);

            if (provider.WaveFormat.SampleRate != AudioFileDecoder.TargetSampleRate)
                provider = new WdlResamplingSampleProvider(provider, AudioFileDecoder.TargetSampleRate);

            long estimatedSamples = (long)(waveStream.TotalTime.TotalSeconds * AudioFileDecoder.TargetSampleRate * AudioFileDecoder.TargetChannels)
                                    + AudioFileDecoder.TargetSampleRate * AudioFileDecoder.TargetChannels; // +1 sec pad
            var samples = new float[estimatedSamples];
            int filled = 0;
            int read;
            while ((read = provider.Read(samples, filled, samples.Length - filled)) > 0)
            {
                filled += read;
                if (filled == samples.Length)
                    Array.Resize(ref samples, samples.Length + samples.Length / 2);
            }

            if (filled != samples.Length) Array.Resize(ref samples, filled);
            return samples;
        }
    }
}
