namespace Sholto.Controller.Gestures;

/// <summary>Turns device-neutral <see cref="ControllerEvent"/>s into intent, in one
/// place, for every consumer.
///
/// <para><b>It reads device state only</b> — Shift, the stem-level modifier, the pad
/// page, platter touch, press duration. It must never see app state such as
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
public sealed class GestureRecognizer
{
    /// <summary>How long the browse knob must be held to mean "re-analyse" rather
    /// than "nothing". Matches the timer this replaces in Orchestrator.</summary>
    public static readonly TimeSpan BrowseHold = TimeSpan.FromSeconds(1);

    private readonly bool[] _shiftHeld = new bool[2];
    private readonly bool[] _platterTouched = new bool[2];
    private readonly PadPage[] _padPage = [PadPage.HotCue, PadPage.HotCue];
    private bool _stemLevelHeld;

    private DateTime? _browsePressedAt;
    private ControllerEvent? _browsePressEvent;
    private bool _browseHoldFired;

    public bool IsShiftHeld(int deck) => deck is 0 or 1 && _shiftHeld[deck];
    public bool IsStemLevelHeld => _stemLevelHeld;
    public bool IsPlatterTouched(int deck) => deck is 0 or 1 && _platterTouched[deck];
    public PadPage PageFor(int deck) => deck is 0 or 1 ? _padPage[deck] : PadPage.HotCue;

    /// <summary>Resolve one event. Returns null when the event carries no gesture —
    /// the raw presses the Controller consumes itself, and the browse press, whose
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

            case ControllerEvent.StemLevelMode sm:
                _stemLevelHeld = sm.Pressed;
                return new Gesture(GestureIds.StemLevelHold, -1, evt);

            case ControllerEvent.JogTouch jt:
                if (jt.Deck is 0 or 1) _platterTouched[jt.Deck] = jt.Touching;
                return new Gesture(GestureIds.JogTopTouch, jt.Deck, evt);

            case ControllerEvent.PadPageSelected pp:
                if (pp.Deck is 0 or 1) _padPage[pp.Deck] = pp.Page;
                return new Gesture(
                    pp.Page == PadPage.PadFx1 ? GestureIds.PadModePadFx1 : GestureIds.PadModeHotCue,
                    pp.Deck, evt);

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

            case ControllerEvent.CueChanged cc:
                return new Gesture(GestureIds.CueHeadphoneToggle, cc.Deck, evt);

            case ControllerEvent.MasterCueChanged:
                return new Gesture(GestureIds.MasterCueToggle, -1, evt);

            case ControllerEvent.BeatSyncPressed bs:
                return new Gesture(GestureIds.SyncPress, bs.Deck, evt);

            case ControllerEvent.CyclePitchRange cpr:
                return new Gesture(GestureIds.SyncCyclePitchRange, cpr.Deck, evt);

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

            case ControllerEvent.EchoToggle et:
                return new Gesture(GestureIds.PadEcho, et.Deck, evt);

            // ---- loops and grid
            case ControllerEvent.BeatLoopToggle bl:
                return new Gesture(GestureIds.BeatLoopToggle, bl.Deck, evt);
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
                    _stemLevelHeld ? GestureIds.EqStemLevelTurn : GestureIds.EqTurn,
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

            // ---- raw presses the Controller consumes itself
            case ControllerEvent.CueToggle:
            case ControllerEvent.MasterCuePressed:
                return null;

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
