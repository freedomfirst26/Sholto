using SoundFlow.Backends.MiniAudio;

namespace Sholto.App.Audio;

/// <summary>Reads the backend miniaudio's context settled on — the one every device opened
/// from <paramref name="engine"/> runs on — and names it the way miniaudio does.
/// <para>SoundFlow 1.4.1's <c>MiniAudioBackend</c> enum cannot be printed as-is: it numbers the
/// backends from 1 (Wasapi = 1 … Oss = 7, PulseAudio = 8) while native miniaudio 0.11's
/// <c>ma_backend</c>, which <c>ActiveBackend</c> carries through unchanged, numbers them from 0
/// (wasapi = 0 … oss = 6, pulseaudio = 7). So PulseAudio prints as "Oss" and WASAPI as
/// "Null". The raw value is the native one, so it is named here from miniaudio's own table.</para></summary>
public sealed class MiniAudioBackendProbe(MiniAudioEngine engine) : IAudioBackendProbe
{
    private readonly MiniAudioEngine _engine = engine;

    public string? ActiveBackend() => NameOf((int)_engine.ActiveBackend);

    /// <summary>miniaudio 0.11's name for a raw <c>ma_backend</c> value (the strings
    /// <c>ma_get_backend_name</c> returns), or null for a value it does not define.</summary>
    public string? NameOf(int maBackend) => maBackend switch
    {
        0 => "WASAPI",
        1 => "DirectSound",
        2 => "WinMM",
        3 => "Core Audio",
        4 => "sndio",
        5 => "audio(4)",
        6 => "OSS",
        7 => "PulseAudio",
        8 => "ALSA",
        9 => "JACK",
        10 => "AAudio",
        11 => "OpenSL|ES",
        12 => "Web Audio",
        13 => "Custom",
        14 => "Null",
        _ => null,
    };
}
