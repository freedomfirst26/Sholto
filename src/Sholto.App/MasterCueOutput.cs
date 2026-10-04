using Sholto.App.Audio;

namespace Sholto.App;

/// <summary>Forwards MASTER CUE to the audio engine once there is one. The engine starts after the
/// device picker, so <see cref="Engine"/> is assigned later (<see cref="Attach"/>); until then a toggle has
/// no audio effect (as before).</summary>
public sealed class MasterCueOutput : IMasterCueOutput, IMasterCueEngineSink
{
    public AudioEngine? Engine { get; set; }

    public void Attach(AudioEngine engine) => Engine = engine;

    public void SetMasterCue(bool on) => Engine?.SetMasterCue(on);
}
