namespace Sholto.Data;

/// <summary>Which pad layout the controller's pads are currently emitting.
/// HotCue = saved hot cues; PadFx1 = echo and roll; BeatJump = beat jumps; Sampler = stem mute and solo.
/// The four Shift pages, reached with Shift + a pad-mode button: Keyboard (Shift + HOT CUE), PadFx2
/// (Shift + PAD FX1), BeatLoop (Shift + BEAT JUMP) = beat loops of 1/4 to 32 beats, KeyShift
/// (Shift + SAMPLER). Keyboard, PadFx2 and KeyShift have no job yet: their pads stay dark and do nothing.
/// Switched by the pad-mode buttons; the App owns the current page.</summary>
public enum PadPage
{
    HotCue,
    PadFx1,
    BeatJump,
    Sampler,
    Keyboard,
    PadFx2,
    BeatLoop,
    KeyShift,
}
