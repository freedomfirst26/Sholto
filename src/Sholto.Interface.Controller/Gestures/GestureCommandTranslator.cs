using Sholto.Data;

namespace Sholto.Interface.Controller.Gestures;

/// <summary>The controller's gesture-to-command map: each gesture becomes one strongly typed command,
/// stamped with where it came from, and goes out on the <see cref="ICommandSender"/>. The payload (jog
/// delta, fader position, stem, bars) is read off the raw event here, so the App never sees a
/// <see cref="ControllerEvent"/>. Device state the App used to read (Shift) is resolved here too and
/// travels on the command.
/// <para>Gestures that mean nothing to the App (a short browse press, plain CUE, plain SYNC, and the two
/// Shift hold, which the recognizer keeps for itself) send a
/// <see cref="ReportControl"/>: it does nothing in the App, but while the guide is open it is echoed like any
/// other command, so the guide can explain the control.</para>
/// <para>Hot path (jog, faders): no allocation per call.</para></summary>
internal sealed class GestureCommandTranslator(ICommandSender sender, IGestureRecognizer recognizer)
    : IGestureCommandTranslator
{
    // Not recognizer gestures: the name stamped on a modifier's release, so the echoed ReportControl tells
    // the guide the modifier was let go (the Faceplate's guide data names only the holds).
    private const string ShiftReleaseName = "shift.release";

    private readonly ICommandSender _sender = sender;
    private readonly IGestureRecognizer _recognizer = recognizer;

    public void Translate(in Gesture g)
    {
        var deck = g.Deck;
        switch (g.Id)
        {
            case GestureIds.PlayPress:
                _sender.Send(new TogglePlay(deck, By("deck.play", g.Id, deck)));
                break;
            case GestureIds.CueTransportRestart:
                _sender.Send(new RestartTrack(deck, By("deck.cue.transport", g.Id, deck)));
                break;
            case GestureIds.CueHeadphoneToggle:
                _sender.Send(new ToggleHeadphoneCue(deck, By("deck.cue.headphone", g.Id, deck)));
                break;
            case GestureIds.MasterCueToggle:
                _sender.Send(new ToggleMasterCue(By("mixer.mastercue", g.Id, Origin.NoDeck)));
                break;
            case GestureIds.SyncCycleTempoRange:
                _sender.Send(new CycleTempoRange(deck, By("deck.sync", g.Id, deck)));
                break;
            case GestureIds.LoadPress:
                _sender.Send(new LoadSelectedIntoDeck(deck, By("deck.load", g.Id, deck)));
                break;

            case GestureIds.JogTopTouch:
                var touch = (ControllerEvent.JogTouch)g.Source;
                _sender.Send(new TouchPlatter(deck, touch.Touching, _recognizer.IsShiftHeld(deck), By("deck.jog", g.Id, deck)));
                break;
            case GestureIds.JogTopTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.Top, false, By("deck.jog", g.Id, deck)));
                break;
            case GestureIds.JogTopShiftTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.Top, true, By("deck.jog", g.Id, deck)));
                break;
            case GestureIds.JogRingTurn:
                _sender.Send(new TurnPlatter(deck, ((ControllerEvent.JogRotated)g.Source).Delta,
                    PlatterSurface.SideRing, false, By("deck.jog", g.Id, deck)));
                break;

            case GestureIds.CrossfaderMove:
                _sender.Send(new SetCrossfader(((ControllerEvent.CrossfaderMoved)g.Source).Position,
                    By("mixer.crossfader", g.Id, Origin.NoDeck)));
                break;
            case GestureIds.VolumeMove:
                _sender.Send(new SetChannelVolume(deck, ((ControllerEvent.ChannelVolumeMoved)g.Source).Value,
                    By("mixer.volume", g.Id, deck)));
                break;
            case GestureIds.EqTurn:
                var eq = (ControllerEvent.EqMoved)g.Source;
                _sender.Send(new SetEq(deck, (int)eq.Band, eq.Value, By(EqControl(eq.Band), g.Id, deck)));
                break;
            case GestureIds.EqStemLevelTurn:
                // HI -> Drums, MID -> Vocals, LOW -> Instrumental, on this deck.
                var level = (ControllerEvent.EqMoved)g.Source;
                var stem = level.Band switch { EqBand.High => 0, EqBand.Mid => 1, _ => 2 };
                // Knob centre = unity (1.0), fully left = silent (0), right of centre stays at unity.
                var stemGain = Math.Min(1.0, level.Value * 2.0);
                _sender.Send(new SetStemLevel(deck, stem, stemGain, By(EqControl(level.Band), g.Id, deck)));
                break;
            case GestureIds.FilterTurn:
                _sender.Send(new SetFilter(deck, ((ControllerEvent.FilterMoved)g.Source).Position,
                    By("mixer.filter", g.Id, deck)));
                break;
            case GestureIds.TempoMove:
                _sender.Send(new SetTempo(deck, ((ControllerEvent.TempoMoved)g.Source).Position,
                    By("mixer.tempo", g.Id, deck)));
                break;

            case GestureIds.PadHotCuePress:
                var press = (ControllerEvent.HotCuePad)g.Source;
                _sender.Send(new PressHotCue(deck, press.Pad, press.Pressed, By(PadControl(press.Pad), g.Id, deck)));
                break;
            case GestureIds.PadHotCueDelete:
                var delete = (ControllerEvent.HotCuePad)g.Source;
                _sender.Send(new DeleteHotCue(deck, delete.Pad, By(PadControl(delete.Pad), g.Id, deck)));
                break;
            case GestureIds.PadBeatJump:
                var jump = (ControllerEvent.BeatJumpPad)g.Source;
                _sender.Send(new BeatJump(deck, jump.Beats, By(PadControl(jump.Pad), g.Id, deck)));
                break;
            case GestureIds.PadBeatJumpHalve:
                var halve = (ControllerEvent.BeatJumpPad)g.Source;
                _sender.Send(new HalveBeatJump(deck, By(PadControl(halve.Pad), g.Id, deck)));
                break;
            case GestureIds.PadBeatJumpDouble:
                var grow = (ControllerEvent.BeatJumpPad)g.Source;
                _sender.Send(new DoubleBeatJump(deck, By(PadControl(grow.Pad), g.Id, deck)));
                break;
            case GestureIds.PadStemSoloDrums:
            case GestureIds.PadStemSoloVocals:
            case GestureIds.PadStemSoloInstrumental:
                var solo = ((ControllerEvent.StemSolo)g.Source).Group;
                _sender.Send(new SoloStem(deck, solo, By(solo switch { 0 => "deck.pad.5", 1 => "deck.pad.6", _ => "deck.pad.7" }, g.Id, deck)));
                break;
            case GestureIds.PadStemDrums:
            case GestureIds.PadStemVocals:
            case GestureIds.PadStemInstrumental:
                // Which stem toggles is read from the event's own group, not from which id fired.
                var group = ((ControllerEvent.StemToggle)g.Source).Group;
                _sender.Send(new ToggleStem(deck, group, By(group switch { 0 => "deck.pad.1", 1 => "deck.pad.2", _ => "deck.pad.3" }, g.Id, deck)));
                break;
            case GestureIds.PadEcho:
                _sender.Send(new HoldEcho(deck, ((ControllerEvent.EchoHold)g.Source).Pressed, By("deck.pad.1", g.Id, deck)));
                break;
            case GestureIds.PadRoll:
                _sender.Send(new HoldRoll(deck, ((ControllerEvent.RollHold)g.Source).Pressed, By("deck.pad.2", g.Id, deck)));
                break;
            case GestureIds.PadModeHotCue:
            case GestureIds.PadModePadFx1:
            case GestureIds.PadModeBeatJump:
            case GestureIds.PadModeSampler:
                // The page is read off the event: with Shift, the same button selects its Shift page.
                var page = ((ControllerEvent.PadPageSelected)g.Source).Page;
                _sender.Send(new SelectPadPage(deck, page, By(PadModeControl(g.Id), g.Id, deck)));
                break;

            case GestureIds.BeatLoopToggle:
                if (g.Source is ControllerEvent.BeatLoopPad loopPad)
                    _sender.Send(new ToggleBeatLoop(deck, 0, By(PadControl(loopPad.Pad), g.Id, deck), loopPad.Beats));
                else
                    _sender.Send(new ToggleBeatLoop(deck, ((ControllerEvent.BeatLoopToggle)g.Source).Bars,
                        By("deck.beatloop.toggle", g.Id, deck)));
                break;
            case GestureIds.BeatLoopHalve:
                _sender.Send(new HalveLoop(deck, By("deck.beatloop.halve", g.Id, deck)));
                break;
            case GestureIds.BeatLoopDouble:
                _sender.Send(new DoubleLoop(deck, By("deck.beatloop.double", g.Id, deck)));
                break;
            case GestureIds.GridNudgeBack:
            case GestureIds.GridNudgeForward:
                _sender.Send(new NudgeGrid(NudgeDeck(deck), ((ControllerEvent.NudgeGrid)g.Source).Beats,
                    By(g.Id == GestureIds.GridNudgeBack ? "beat.nudge.back" : "beat.nudge.forward", g.Id, Origin.NoDeck)));
                break;

            case GestureIds.BrowseTurn:
                _sender.Send(new RotateBrowse(((ControllerEvent.BrowseRotated)g.Source).Delta, By("browse.knob", g.Id, Origin.NoDeck)));
                break;
            case GestureIds.BrowsePressHold:
                _sender.Send(new ReanalyzeSelected(By("browse.knob", g.Id, Origin.NoDeck)));
                break;

            // No effect on the App: reported only, for the guide. The Shift modifier is
            // held state the recognizer keeps; its release carries its own gesture name because the
            // echo has no payload.
            case GestureIds.BrowsePressShort:
                _sender.Send(new ReportControl(true, By("browse.knob", g.Id, Origin.NoDeck)));
                break;
            case GestureIds.CueTransportPlain:
                _sender.Send(new ReportControl(true, By("deck.cue.transport", g.Id, deck)));
                break;
            case GestureIds.SyncPress:
                _sender.Send(new ReportControl(true, By("deck.sync", g.Id, deck)));
                break;
            case GestureIds.ShiftHold:
                var shift = ((ControllerEvent.DeckShift)g.Source).Pressed;
                _sender.Send(new ReportControl(shift, By("deck.shift", shift ? g.Id : ShiftReleaseName, deck)));
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

    /// <summary>The guide's control id of the pad-mode button behind a pad-mode gesture.</summary>
    private string PadModeControl(string gestureId) => gestureId switch
    {
        GestureIds.PadModePadFx1 => "deck.padmode.padfx1",
        GestureIds.PadModeBeatJump => "deck.padmode.beatjump",
        GestureIds.PadModeSampler => "deck.padmode.sampler",
        _ => "deck.padmode.hotcue",
    };

    /// <summary>The guide's control id of pad <paramref name="pad"/> (0-7): <c>deck.pad.1</c> to <c>deck.pad.8</c>.</summary>
    private string PadControl(int pad) => $"deck.pad.{pad + 1}";

    private string EqControl(EqBand band) => band switch
    {
        EqBand.High => "mixer.eq.hi",
        EqBand.Mid => "mixer.eq.mid",
        _ => "mixer.eq.low",
    };

    private Origin By(string controlId, string gestureName, int deck) =>
        new(InterfaceIds.Controller, controlId, gestureName, deck);
}
