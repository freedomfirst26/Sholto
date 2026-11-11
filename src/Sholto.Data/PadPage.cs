namespace Sholto.Data;

/// <summary>Which pad layout the controller's pads are currently emitting.
/// HotCue = saved hot cues; PadFx1 = echo and roll; BeatJump = beat jumps; Sampler = stem mute and solo.
/// Switched by the four pad-mode buttons; the App owns the current page.</summary>
public enum PadPage
{
    HotCue,
    PadFx1,
    BeatJump,
    Sampler,
}
