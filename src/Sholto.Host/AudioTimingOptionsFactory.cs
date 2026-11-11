using Sholto.App.Audio;
namespace Sholto.Host;

/// <summary>Builds the <see cref="AudioTimingOptions"/>: off, unless the <c>SHOLTO_AUDIO_TIMING</c>
/// environment variable is exactly <c>1</c>. Called once, from <see cref="SholtoOptions"/>.</summary>
public sealed class AudioTimingOptionsFactory(Func<string, string?> env) : IAudioTimingOptionsFactory
{
    private readonly Func<string, string?> _env = env;

    public AudioTimingOptions Create() => new() { Enabled = _env("SHOLTO_AUDIO_TIMING") == "1" };
}
