using Sholto.Data;

namespace Sholto.Interface.Controller.Gestures;

/// <summary>The controller's gesture-to-command map: each gesture becomes one strongly typed command,
/// stamped with where it came from, and goes out on the <see cref="ICommandSender"/>. The payload (jog
/// delta, fader position, stem, bars) is read off the raw event here, so the App never sees a
/// <see cref="ControllerEvent"/>. Device state the App used to read (Shift) is resolved here too and
/// travels on the command.
/// <para>Gestures that mean nothing to the App (plain CUE, plain SYNC, and the two
/// modifier holds, Shift and stem-level, which the recognizer keeps for itself) send a
/// <see cref="ReportControl"/>: it does nothing in the App, but while the guide is open it is echoed like any
/// other command, so the guide can explain the control.</para>
/// <para>Hot path (jog, faders): no allocation per call.</para></summary>
internal sealed class GestureCommandTranslator(ICommandSender sender, IGestureRecognizer recognizer)
    : IGestureCommandTranslator
{
    // Not recognizer gestures: the name stamped on a modifier's release, so the echoed ReportControl tells
    // the guide the modifier was let go (the Faceplate's guide data names only the holds).
    private const string ShiftReleaseName = "shift.release";
    private const string StemLevelReleaseName = "stemlevel.release";

    private readonly ICommandSender _sender = sender;
    private readonly IGestureRecognizer _recognizer = recognizer;

    public void Translate(in Gesture g)
    {
        var deck = g.Deck;
        switch (g.Id)
        {
            case GestureIds.PlayPress:
                _sender.Send(new TogglePlay(deck, By("deck.play", g.Id)));
                break;
            case GestureIds.CueTransportRestart:
                _sender.Send(new RestartTrack(deck, By("deck.cue.transport", g.Id)));
                break;
            case GestureIds.CueHeadphoneToggle:
                _sender.Send(new ToggleHeadphoneCue(deck, By("deck.cue.headphone", g.Id)));
                break;
            case GestureIds.MasterCueToggle:
                _sender.Send(new ToggleMasterCue(By("mixer.mastercue", g.Id)));
                break;
            case GestureIds.SyncCycleTempoRange:
                _sender.Send(new CycleTempoRange(deck, By("deck.sync", g.Id)));
                break;
            case GestureIds.LoadPress:
                _sender.Send(new LoadSelectedIntoDeck(deck, By("deck.load", g.Id)));
                break;

            case GestureIds.JogTopTouch:
                var touch = (ControllerEvent.JogTouch)g.Source;
                _sender.Send(new TouchPlatter(deck, touch.Touching, _recognizer.IsShiftHeld(deck), By("deck.jog", g.Id)));
                break;
            case GestureIds.JogTopTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.Top, false, By("deck.jog", g.Id)));
                break;
            case GestureIds.JogTopShiftTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.Top, true, By("deck.jog", g.Id)));
                break;
            case GestureIds.JogRingTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.SideRing, false, By("deck.jog", g.Id)));
                break;

            case GestureIds.CrossfaderMove:
                _sender.Send(new SetCrossfader(((ControllerEvent.CrossfaderMoved)g.Source).Position,
                    By("mixer.crossfader", g.Id)));
                break;
            case GestureIds.VolumeMove:
                _sender.Send(new SetChannelVolume(deck, ((ControllerEvent.ChannelVolumeMoved)g.Source).Value,
                    By("mixer.volume", g.Id)));
                break;
            case GestureIds.EqTurn:
                var eq = (ControllerEvent.EqMoved)g.Source;
                _sender.Send(new SetEq(deck, (int)eq.Band, eq.Value, By(EqControl(eq.Band), g.Id)));
                break;
            case GestureIds.EqStemLevelTurn:
                // HI -> Drums, MID -> Vocals, LOW -> Instrumental, on either deck.
                var level = (ControllerEvent.EqMoved)g.Source;
                var stem = level.Band switch { EqBand.High => 0, EqBand.Mid => 1, _ => 2 };
                _sender.Send(new SetStemLevel(deck, stem, level.Value, By(EqControl(level.Band), g.Id)));
                break;
            case GestureIds.FilterTurn:
                _sender.Send(new SetFilter(deck, ((ControllerEvent.FilterMoved)g.Source).Position,
                    By("mixer.filter", g.Id)));
                break;
            case GestureIds.TempoMove:
                _sender.Send(new SetTempo(deck, ((ControllerEvent.TempoMoved)g.Source).Position,
                    By("mixer.tempo", g.Id)));
                break;

            case GestureIds.PadStemDrums:
            case GestureIds.PadStemVocals:
            case GestureIds.PadStemInstrumental:
                // Which stem toggles is read from the event's own group, not from which id fired.
                var group = ((ControllerEvent.StemToggle)g.Source).Group;
                _sender.Send(new ToggleStem(deck, group, By(group switch { 0 => "deck.pad.1", 1 => "deck.pad.2", _ => "deck.pad.3" }, g.Id)));
                break;
            case GestureIds.PadEcho:
                _sender.Send(new ToggleEcho(deck, By("deck.pad.1", g.Id)));
                break;
            case GestureIds.PadRoll:
                _sender.Send(new HoldRoll(deck, ((ControllerEvent.RollHold)g.Source).Pressed, By("deck.pad.2", g.Id)));
                break;
            case GestureIds.PadModeHotCue:
                _sender.Send(new SelectPadPage(deck, PadPage.HotCue, By("deck.padmode.hotcue", g.Id)));
                break;
            case GestureIds.PadModePadFx1:
                _sender.Send(new SelectPadPage(deck, PadPage.PadFx1, By("deck.padmode.padfx1", g.Id)));
                break;

            case GestureIds.BeatLoopToggle:
                _sender.Send(new ToggleBeatLoop(deck, ((ControllerEvent.BeatLoopToggle)g.Source).Bars,
                    By("deck.beatloop.toggle", g.Id)));
                break;
            case GestureIds.BeatLoopHalve:
                _sender.Send(new HalveLoop(deck, By("deck.beatloop.halve", g.Id)));
                break;
            case GestureIds.BeatLoopDouble:
                _sender.Send(new DoubleLoop(deck, By("deck.beatloop.double", g.Id)));
                break;
            case GestureIds.GridNudgeBack:
            case GestureIds.GridNudgeForward:
                _sender.Send(new NudgeGrid(NudgeDeck(deck), ((ControllerEvent.NudgeGrid)g.Source).Beats,
                    By(g.Id == GestureIds.GridNudgeBack ? "beat.nudge.back" : "beat.nudge.forward", g.Id)));
                break;

            case GestureIds.BrowseTurn:
                _sender.Send(new RotateBrowse(((ControllerEvent.BrowseRotated)g.Source).Delta, By("browse.knob", g.Id)));
                break;
            case GestureIds.BrowsePressHold:
                _sender.Send(new ReanalyzeSelected(By("browse.knob", g.Id)));
                break;
            case GestureIds.BrowsePressShort:
                _sender.Send(new OpenSearch(By("browse.knob", g.Id)));
                break;

            // No effect on the App: reported only, for the guide. The modifiers (Shift, stem-level) are
            // held state the recognizer keeps; their release carries its own gesture name because the
            // echo has no payload.
            case GestureIds.CueTransportPlain:
                _sender.Send(new ReportControl(deck, true, By("deck.cue.transport", g.Id)));
                break;
            case GestureIds.SyncPress:
                _sender.Send(new ReportControl(deck, true, By("deck.sync", g.Id)));
                break;
            case GestureIds.ShiftHold:
                var shift = ((ControllerEvent.DeckShift)g.Source).Pressed;
                _sender.Send(new ReportControl(deck, shift, By("deck.shift", shift ? g.Id : ShiftReleaseName)));
                break;
            case GestureIds.StemLevelHold:
                var stemLevel = ((ControllerEvent.StemLevelMode)g.Source).Pressed;
                _sender.Send(new ReportControl(deck, stemLevel, By("fx.onoff", stemLevel ? g.Id : StemLevelReleaseName)));
                break;
        }
    }

    /// <summary>The BEAT arrows are one pair shared by both decks, so they arrive deckless. Pick the deck
    /// whose Shift is held (deck 0 first); with none held, -1 tells the App to choose.</summary>
    private int NudgeDeck(int deck)
    {
        if (deck >= 0) return deck;
        if (_recognizer.IsShiftHeld(0)) return 0;
        if (_recognizer.IsShiftHeld(1)) return 1;
        return -1;
    }

    private string EqControl(EqBand band) => band switch
    {
        EqBand.High => "mixer.eq.hi",
        EqBand.Mid => "mixer.eq.mid",
        _ => "mixer.eq.low",
    };

    private Origin By(string controlId, string gestureName) => new(InterfaceIds.Controller, controlId, gestureName);
}
