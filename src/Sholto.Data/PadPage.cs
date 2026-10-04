namespace Sholto.Data;

/// <summary>Which pad layout the controller's pads are currently emitting.
/// HotCue = pads 0x00-0x07 (stem mutes on pads 1-3); PadFx1 = pads 0x10-0x17
/// (echo toggle on pad 1). Switched by the HOT CUE / PAD FX1 mode buttons.
/// BeatJump and Sampler are the BEAT JUMP and SAMPLER pad modes, not yet wired to MIDI.</summary>
public enum PadPage
{
    HotCue,
    PadFx1,
    BeatJump,
    Sampler,
}
