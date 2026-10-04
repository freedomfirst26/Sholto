using Sholto.App.Audio;

namespace Sholto.App;

/// <summary>Told which audio engine to forward MASTER CUE to, once the engine exists.</summary>
public interface IMasterCueEngineSink
{
    void Attach(AudioEngine engine);
}
