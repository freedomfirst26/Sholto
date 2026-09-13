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
///
/// The strategy list (and, via <see cref="FlacDecodeStrategy"/>, the SoundFlow engine
/// handle) is supplied by the caller — composed once at the top in App.axaml.cs — rather
/// than reached through a global.
/// </summary>
public sealed class AudioFileDecoder : IAudioFileDecoder
{
    // Match AudioEngine output rate so SoundFlow doesn't have to resample on
    // playback — a rate mismatch here makes the audio play at engineRate/sourceRate
    // speed (e.g. 48000/44100 = 8.8% too fast).
    public const int TargetSampleRate = 48000;
    public const int TargetChannels = 2;

    private readonly IAudioDecodeStrategy[] _strategies;

    public AudioFileDecoder(IAudioDecodeStrategy[] strategies)
    {
        _strategies = strategies;
    }

    public float[] Decode(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        var strategy = Array.Find(_strategies, s => s.CanDecode(ext))
            ?? throw new NotSupportedException($"No decode strategy for '{ext}' ({filePath}).");
        return strategy.Decode(filePath);
    }
}
