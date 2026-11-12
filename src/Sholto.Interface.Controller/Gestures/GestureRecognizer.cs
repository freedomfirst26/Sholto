using Sholto.Data;

namespace Sholto.Interface.Controller.Gestures;

/// <summary>Turns device-neutral <see cref="ControllerEvent"/>s into intent, in one
/// place, for every consumer.
///
/// <para><b>It reads device state only</b> — Shift, platter touch, press duration. It must never see app state such as
/// <c>Player.CanScratch</c> or <c>ActiveLoop</c>. <c>jog.top.turn</c> is what the DJ
/// did; deciding to scratch or to seek is what Sholto does about it, and that stays
/// in the Orchestrator.</para>
///
/// <para><b>It never reads raw MIDI.</b> Wire numbers live in the device mapping and
/// nowhere else, so a second controller producing the same events reuses this class
/// unchanged.</para>
///
/// <para>Not thread-safe. Call it on the UI thread, as the App already does.</para>
/// </summary>
internal sealed class GestureRecognizer : IGestureRecognizer
{
    /// <summary>How long the browse knob must be held to mean "re-analyse" rather
    /// than "nothing". Matches the timer this replaces in Orchestrator.</summary>
    public readonly TimeSpan BrowseHold = TimeSpan.FromSeconds(1);

    /// <summary>The BEAT JUMP pads (0-based) that, with Shift, halve and double the jump size: pads 7 and 8,
    /// as on Rekordbox's FLX4 layout.</summary>
    private const int BeatJumpHalvePad = 6;
    private const int BeatJumpDoublePad = 7;

    private readonly bool[] _shiftHeld = new bool[2];
    private readonly bool[] _platterTouched = new bool[2];

    private DateTime? _browsePressedAt;
    private ControllerEvent? _browsePressEvent;
    private bool _browseHoldFired;

    public bool IsShiftHeld(int deck) => deck is 0 or 1 && _shiftHeld[deck];
    public bool IsPlatterTouched(int deck) => deck is 0 or 1 && _platterTouched[deck];

    /// <summary>Resolve one event. Returns null when the event carries no gesture —
    /// the browse press, whose
    /// meaning is not known until it is released or held long enough.</summary>
    public Gesture? Recognize(ControllerEvent evt, DateTime nowUtc)
    {
        switch (evt)
        {
            // ---- modifiers: state first, then report them as gestures of their own
            // so the Faceplate can light SHIFT while it is held.
            case ControllerEvent.DeckShift ds:
                if (ds.Deck is 0 or 1) _shiftHeld[ds.Deck] = ds.Pressed;
                return new Gesture(GestureIds.ShiftHold, ds.Deck, evt);

            case ControllerEvent.JogTouch jt:
                if (jt.Deck is 0 or 1) _platterTouched[jt.Deck] = jt.Touching;
                return new Gesture(GestureIds.JogTopTouch, jt.Deck, evt);

            // A Shift page is the same physical button as its plain page (Shift + BEAT JUMP = BEAT LOOP), so it
            // is that button's gesture; the translator reads the page itself off the event.
            case ControllerEvent.PadPageSelected pp:
                return new Gesture(pp.Page switch
                {
                    PadPage.PadFx1 or PadPage.PadFx2 => GestureIds.PadModePadFx1,
                    PadPage.BeatJump or PadPage.BeatLoop => GestureIds.PadModeBeatJump,
                    PadPage.Sampler or PadPage.KeyShift => GestureIds.PadModeSampler,
                    _ => GestureIds.PadModeHotCue,
                }, pp.Deck, evt);

            // ---- transport
            case ControllerEvent.PlayPressed p:
                return new Gesture(GestureIds.PlayPress, p.Deck, evt);

            case ControllerEvent.TransportCuePressed tc:
                // Shifted comes from the wire, because the firmware remaps the chord
                // to its own note. The held-state check is the fallback for a
                // firmware that sends the plain note instead.
                return new Gesture(
                    tc.Shifted || IsShiftHeld(tc.Deck)
                        ? GestureIds.CueTransportRestart
                        : GestureIds.CueTransportPlain,
                    tc.Deck, evt);

            case ControllerEvent.CueToggle ct:
                return new Gesture(GestureIds.CueHeadphoneToggle, ct.Deck, evt);

            case ControllerEvent.MasterCuePressed:
                return new Gesture(GestureIds.MasterCueToggle, -1, evt);

            case ControllerEvent.BeatSyncPressed bs:
                return new Gesture(GestureIds.SyncPress, bs.Deck, evt);

            case ControllerEvent.CycleTempoRange cpr:
                return new Gesture(GestureIds.SyncCycleTempoRange, cpr.Deck, evt);

            case ControllerEvent.LoadToDeck l:
                return new Gesture(GestureIds.LoadPress, l.Deck, evt);

            // ---- pads
            case ControllerEvent.StemToggle st:
                return new Gesture(st.Group switch
                {
                    0 => GestureIds.PadStemDrums,
                    1 => GestureIds.PadStemVocals,
                    _ => GestureIds.PadStemInstrumental,
                }, st.Deck, evt);

            case ControllerEvent.HotCuePad hc:
                // Shift + pad deletes. Shifted comes from the wire (the firmware remaps the chord to its
                // own notes); the held-state check is the fallback. A release is always the plain press's
                // second edge, so a preview started unshifted still ends if Shift went down meanwhile; a
                // shifted release means nothing.
                if (!hc.Pressed)
                    return hc.Shifted ? null : new Gesture(GestureIds.PadHotCuePress, hc.Deck, evt);
                return new Gesture(
                    hc.Shifted || IsShiftHeld(hc.Deck) ? GestureIds.PadHotCueDelete : GestureIds.PadHotCuePress,
                    hc.Deck, evt);

            case ControllerEvent.BeatJumpPad bj:
                // Shift + pad 7 halves and Shift + pad 8 doubles every pad's jump (Rekordbox's FLX4 layout);
                // Shift + any other BEAT JUMP pad means nothing. Shifted comes from the wire; the held-state
                // check is the fallback, as for the HOT CUE pads.
                if (!bj.Shifted && !IsShiftHeld(bj.Deck))
                    return new Gesture(GestureIds.PadBeatJump, bj.Deck, evt);
                return bj.Pad switch
                {
                    BeatJumpHalvePad => new Gesture(GestureIds.PadBeatJumpHalve, bj.Deck, evt),
                    BeatJumpDoublePad => new Gesture(GestureIds.PadBeatJumpDouble, bj.Deck, evt),
                    _ => null,
                };

            case ControllerEvent.StemSolo ss:
                return new Gesture(ss.Group switch
                {
                    0 => GestureIds.PadStemSoloDrums,
                    1 => GestureIds.PadStemSoloVocals,
                    _ => GestureIds.PadStemSoloInstrumental,
                }, ss.Deck, evt);

            case ControllerEvent.EchoHold eh:
                return new Gesture(GestureIds.PadEcho, eh.Deck, evt);

            case ControllerEvent.RollHold rh:
                return new Gesture(GestureIds.PadRoll, rh.Deck, evt);

            // ---- loops and grid
            case ControllerEvent.BeatLoopToggle bl:
                return new Gesture(GestureIds.BeatLoopToggle, bl.Deck, evt);
            // A BEAT LOOP pad does what the 4 BEAT button does, at the pad's own length.
            case ControllerEvent.BeatLoopPad lp:
                return new Gesture(GestureIds.BeatLoopToggle, lp.Deck, evt);
            case ControllerEvent.BeatLoopHalve bh:
                return new Gesture(GestureIds.BeatLoopHalve, bh.Deck, evt);
            case ControllerEvent.BeatLoopDouble bd:
                return new Gesture(GestureIds.BeatLoopDouble, bd.Deck, evt);

            case ControllerEvent.NudgeGrid n:
                // Deck stays as sent (-1 for the shared BEAT arrows). Choosing a deck
                // is the Orchestrator's job, not the recognizer's.
                return new Gesture(
                    n.Beats < 0 ? GestureIds.GridNudgeBack : GestureIds.GridNudgeForward,
                    n.Deck, evt);

            // ---- continuous controls
            case ControllerEvent.EqMoved eq:
                return new Gesture(
                    IsShiftHeld(eq.Deck) ? GestureIds.EqStemLevelTurn : GestureIds.EqTurn,
                    eq.Deck, evt);

            case ControllerEvent.FilterMoved f:
                return new Gesture(GestureIds.FilterTurn, f.Deck, evt);
            case ControllerEvent.ChannelVolumeMoved v:
                return new Gesture(GestureIds.VolumeMove, v.Deck, evt);
            case ControllerEvent.TempoMoved t:
                return new Gesture(GestureIds.TempoMove, t.Deck, evt);
            case ControllerEvent.CrossfaderMoved:
                return new Gesture(GestureIds.CrossfaderMove, -1, evt);

            // ---- browse
            case ControllerEvent.BrowseRotated:
                return new Gesture(GestureIds.BrowseTurn, -1, evt);

            case ControllerEvent.BrowsePressed:
                // A press alone means nothing yet. It becomes "short" on release or
                // "hold" once BrowseHold elapses, whichever happens first. Ignore a
                // repeat while already down: some firmwares retransmit NoteOn.
                if (_browsePressedAt is null)
                {
                    _browsePressedAt = nowUtc;
                    _browsePressEvent = evt;
                    _browseHoldFired = false;
                }
                return null;

            case ControllerEvent.BrowseReleased:
            {
                bool fired = _browseHoldFired;
                _browsePressedAt = null;
                _browsePressEvent = null;
                _browseHoldFired = false;
                return fired ? null : new Gesture(GestureIds.BrowsePressShort, -1, evt);
            }

            // ---- jog wheel
            case ControllerEvent.JogRotated j:
                return new Gesture(
                    j.Source == JogSource.SideRing ? GestureIds.JogRingTurn
                    : IsShiftHeld(j.Deck)          ? GestureIds.JogTopShiftTurn
                    :                                GestureIds.JogTopTurn,
                    j.Deck, evt);

            default:
                return null;   // Unrecognised event carries no gesture
        }
    }

    /// <summary>Gestures that became due through time alone. Pump this from the
    /// existing 16 ms tick. Returns an empty list on almost every call.</summary>
    public IReadOnlyList<Gesture> Tick(DateTime nowUtc)
    {
        if (_browsePressedAt is null || _browseHoldFired) return [];
        if (nowUtc - _browsePressedAt.Value < BrowseHold) return [];

        _browseHoldFired = true;
        return [new Gesture(GestureIds.BrowsePressHold, -1, _browsePressEvent!)];
    }
}
