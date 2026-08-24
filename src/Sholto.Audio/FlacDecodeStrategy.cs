using SoundFlow.Providers;
using SoundFlow.Utils;

namespace Sholto.Audio;

/// <summary>FLAC via SoundFlow/miniaudio (dr_flac), decoded in-process. NAudio would
/// route FLAC through MediaFoundation, which is absent on Linux; miniaudio decodes it
/// natively. Decoding targets the deck format (48 kHz stereo F32) directly, so the
/// output matches the NAudio strategies without a separate resample pass.</summary>
public sealed class FlacDecodeStrategy : IAudioDecodeStrategy
{
    public bool CanDecode(string extension) => extension == ".flac";

    public float[] Decode(string filePath)
    {
        var engine = AudioFileDecoder.SoundFlowEngine
            ?? throw new InvalidOperationException(
                "FLAC decoding needs the SoundFlow engine, which AudioEngine sets on startup — " +
                "it isn't initialised yet.");

        using var fs = File.OpenRead(filePath);
        // Target the deck format so the decode resamples/downmixes to 48 kHz stereo F32.
        using var provider = new AssetDataProvider(engine, AudioEngine.DeckFormat, fs);

        int total = provider.Length;               // interleaved samples at the target format
        var buf = new float[total];
        int filled = 0, read;
        while (filled < total && (read = provider.ReadBytes(buf.AsSpan(filled))) > 0)
            filled += read;
        if (filled != total) Array.Resize(ref buf, filled);

        // Safety net: if the provider didn't resample to our rate, convert now so
        // playback speed and pitch stay correct.
        if (provider.SampleRate != AudioFileDecoder.TargetSampleRate && provider.SampleRate > 0)
            buf = MathHelper.ResampleLinear(buf, AudioFileDecoder.TargetChannels,
                                            provider.SampleRate, AudioFileDecoder.TargetSampleRate);

        return buf;
    }
}
