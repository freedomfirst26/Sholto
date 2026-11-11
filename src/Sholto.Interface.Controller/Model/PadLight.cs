namespace Sholto.Interface.Controller.Model;

/// <summary>What a pad shows, logically and device-neutral; the device mapping decides the colour on the
/// hardware. The eight <c>Cue*</c> members sit in <c>CueColour</c> order, so a hot cue maps to its light by
/// offset (<c>CuePink + (int)colour</c>), never by name.</summary>
public enum PadLight
{
    Off,
    CuePink,
    CueRed,
    CueOrange,
    CueYellow,
    CueGreen,
    CueAqua,
    CueBlue,
    CuePurple,
    Echo,
    Roll,
    BeatJump,
    Drums,
    Vocals,
    Instrumental,
}
