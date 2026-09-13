namespace Sholto.Controller.Gestures;

/// <summary>Every gesture the recognizer can emit. These ids are the join between
/// the code that resolves a gesture and the data file that describes it, so treat
/// them as a published contract: renaming one breaks the Faceplate data file, and
/// the Faceplate's coverage test will say so.</summary>
public static class GestureIds
{
    // Transport
    public const string PlayPress            = "play.press";
    public const string CueTransportPlain    = "cue.transport.press";
    public const string CueTransportRestart  = "cue.transport.shift";
    public const string CueHeadphoneToggle   = "cue.headphone.press";
    public const string MasterCueToggle      = "mastercue.press";
    public const string SyncPress            = "sync.press";
    public const string SyncCycleTempoRange  = "sync.shift";
    public const string LoadPress            = "load.press";

    // Jog
    public const string JogTopTurn           = "jog.top.turn";
    public const string JogTopShiftTurn      = "jog.top.shift.turn";
    public const string JogTopTouch          = "jog.top.touch";
    public const string JogRingTurn          = "jog.ring.turn";

    // Mixer
    public const string EqTurn               = "eq.turn";
    public const string EqStemLevelTurn      = "eq.stemlevel.turn";
    public const string FilterTurn           = "filter.turn";
    public const string VolumeMove           = "volume.move";
    public const string TempoMove            = "tempo.move";
    public const string CrossfaderMove       = "crossfader.move";

    // Pads
    public const string PadStemDrums         = "pad.hotcue.drums";
    public const string PadStemVocals        = "pad.hotcue.vocals";
    public const string PadStemInstrumental  = "pad.hotcue.instrumental";
    public const string PadEcho              = "pad.padfx1.echo";
    public const string PadRoll              = "pad.padfx1.roll";
    public const string PadModeHotCue        = "padmode.hotcue.press";
    public const string PadModePadFx1        = "padmode.padfx1.press";

    // Loops and grid
    public const string BeatLoopToggle       = "beatloop.toggle";
    public const string BeatLoopHalve        = "beatloop.halve";
    public const string BeatLoopDouble       = "beatloop.double";
    public const string GridNudgeBack        = "gridnudge.back";
    public const string GridNudgeForward     = "gridnudge.forward";

    // Browse
    public const string BrowseTurn           = "browse.turn";
    public const string BrowsePressShort     = "browse.press.short";
    public const string BrowsePressHold      = "browse.press.hold";

    // Modifiers — emitted so the Faceplate can show them being held.
    public const string ShiftHold            = "shift.hold";
    public const string StemLevelHold        = "stemlevel.hold";

    public static readonly IReadOnlyList<string> All =
    [
        PlayPress, CueTransportPlain, CueTransportRestart, CueHeadphoneToggle,
        MasterCueToggle, SyncPress, SyncCycleTempoRange, LoadPress,
        JogTopTurn, JogTopShiftTurn, JogTopTouch, JogRingTurn,
        EqTurn, EqStemLevelTurn, FilterTurn, VolumeMove, TempoMove, CrossfaderMove,
        PadStemDrums, PadStemVocals, PadStemInstrumental, PadEcho, PadRoll,
        PadModeHotCue, PadModePadFx1,
        BeatLoopToggle, BeatLoopHalve, BeatLoopDouble, GridNudgeBack, GridNudgeForward,
        BrowseTurn, BrowsePressShort, BrowsePressHold,
        ShiftHold, StemLevelHold,
    ];
}
