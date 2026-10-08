namespace Sholto.Interface.Controller.Gestures;

/// <summary>The production <see cref="IGestureCatalog"/>: every id declared on
/// <see cref="GestureIds"/>. Keep in step with that class (GestureIdsTests checks
/// the two agree).</summary>
internal sealed class GestureCatalog : IGestureCatalog
{
    public IReadOnlyList<string> All { get; } =
    [
        GestureIds.PlayPress, GestureIds.CueTransportPlain, GestureIds.CueTransportRestart,
        GestureIds.CueHeadphoneToggle, GestureIds.MasterCueToggle, GestureIds.SyncPress,
        GestureIds.SyncCycleTempoRange, GestureIds.LoadPress, GestureIds.JogTopTurn,
        GestureIds.JogTopShiftTurn, GestureIds.JogTopTouch, GestureIds.JogRingTurn,
        GestureIds.EqTurn, GestureIds.EqStemLevelTurn, GestureIds.FilterTurn,
        GestureIds.VolumeMove, GestureIds.TempoMove, GestureIds.CrossfaderMove,
        GestureIds.PadStemDrums, GestureIds.PadStemVocals, GestureIds.PadStemInstrumental,
        GestureIds.PadEcho, GestureIds.PadRoll, GestureIds.PadModeHotCue, GestureIds.PadModePadFx1,
        GestureIds.BeatLoopToggle, GestureIds.BeatLoopHalve, GestureIds.BeatLoopDouble,
        GestureIds.GridNudgeBack, GestureIds.GridNudgeForward, GestureIds.BrowseTurn,
        GestureIds.BrowsePressShort, GestureIds.BrowsePressHold, GestureIds.ShiftHold,
    ];
}
