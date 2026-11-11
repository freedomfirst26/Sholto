using Sholto.App.Audio;

namespace Sholto.Host;

/// <summary>Builds the audio-timing options.</summary>
public interface IAudioTimingOptionsFactory
{
    AudioTimingOptions Create();
}
