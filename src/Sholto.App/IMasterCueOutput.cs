namespace Sholto.App;

/// <summary>Where MASTER CUE takes effect: the audio engine's master-cue monitor.</summary>
public interface IMasterCueOutput
{
    void SetMasterCue(bool on);
}
