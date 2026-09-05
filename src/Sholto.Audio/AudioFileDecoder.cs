using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Audio;

/// <summary>
/// Decodes an audio file into interleaved float PCM at <see cref="TargetSampleRate"/> /
/// <see cref="TargetChannels"/> — the format both analysis and playback consume.
///
/// Decoding is delegated to a per-format <see cref="IAudioDecodeStrategy"/> because the
/// formats need genuinely different decoders on Linux: MP3 via NLayer (NAudio's own MP3
/// path uses MediaFoundation, which is absent), WAV/AIFF via NAudio's managed readers,
/// FLAC via SoundFlow/miniaudio (NAudio would route FLAC through the missing
/// MediaFoundation too), and M4A/AAC by shelling out to ffmpeg (no in-process decoder
/// in this dependency set handles AAC on Linux, and ffmpeg is already required for the
/// beat detector). Each strategy normalises to the same 48 kHz stereo float array.
/// </summary>
public static class AudioFileDecoder
{
    // Match AudioEngine output rate so SoundFlow doesn't have to resample on
    // playback — a rate mismatch here makes the audio play at engineRate/sourceRate
    // speed (e.g. 48000/44100 = 8.8% too fast).
    public const int TargetSampleRate = 48000;
    public const int TargetChannels = 2;

    /// <summary>The shared SoundFlow engine, needed by <see cref="FlacDecodeStrategy"/>
    /// to decode via miniaudio. Set once when <see cref="AudioEngine"/> starts; FLAC
    /// decoding before that throws with a clear message.</summary>
    public static SfEngine? SoundFlowEngine;

    private static readonly IAudioDecodeStrategy[] Strategies =
    [
        new Mp3DecodeStrategy(),
        new FlacDecodeStrategy(),
        new WavDecodeStrategy(),
        new FfmpegDecodeStrategy(),
    ];

    public static float[] Decode(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        var strategy = Array.Find(Strategies, s => s.CanDecode(ext))
            ?? throw new NotSupportedException($"No decode strategy for '{ext}' ({filePath}).");
        return strategy.Decode(filePath);
    }

    /// <summary>Shared tail for the NAudio-based strategies (MP3, WAV/AIFF): downmix
    /// mono→stereo, resample to <see cref="TargetSampleRate"/>, and read the whole
    /// stream into one float array. TotalTime is only an estimate on VBR sources, so
    /// the buffer is padded and grown if the decoder overruns, then trimmed.</summary>
    internal static float[] ReadNAudioToFloats(WaveStream waveStream)
    {
        using (waveStream)
        {
            ISampleProvider provider = waveStream is ISampleProvider sp
                ? sp
                : waveStream.ToSampleProvider();

            if (provider.WaveFormat.Channels == 1)
                provider = new MonoToStereoSampleProvider(provider);

            if (provider.WaveFormat.SampleRate != TargetSampleRate)
                provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);

            long estimatedSamples = (long)(waveStream.TotalTime.TotalSeconds * TargetSampleRate * TargetChannels)
                                    + TargetSampleRate * TargetChannels; // +1 sec pad
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
