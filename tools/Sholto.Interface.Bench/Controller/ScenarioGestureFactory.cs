using Sholto.Interface.Bench.Scenario;
using Sholto.Data;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Controller;

/// <summary>
/// Turns one "gesture" <see cref="ScenarioAction"/> into the <see cref="ControllerEvent"/>
/// it names — expressed in the same device-neutral vocabulary <see cref="Controller"/>
/// (real hardware) or a mapping's <c>Translate</c> (raw MIDI, see the "midi" action)
/// would have produced. Covers every <c>ControllerEvent</c> record. CUE presses are
/// toggles (<c>CueToggle</c>, <c>MasterCuePressed</c>): the App owns the cue state.
///
/// Deck numbers in scenario JSON stay 1/2 (the same convention load/gain/play
/// already use) and are converted to the 0/1 the rest of the controller stack
/// uses.
/// </summary>
public sealed class ScenarioGestureFactory : IScenarioGestureFactory
{
    public ControllerEvent Create(ScenarioAction a)
    {
        string evt = a.Event!;
        return evt switch
        {
            "PlayPressed" => new ControllerEvent.PlayPressed(Deck0(a)),
            "LoadToDeck" => new ControllerEvent.LoadToDeck(Deck0(a)),
            "CueToggle" => new ControllerEvent.CueToggle(Deck0(a)),
            "MasterCuePressed" => new ControllerEvent.MasterCuePressed(),
            "CrossfaderMoved" => new ControllerEvent.CrossfaderMoved(RequireValue(a)),
            "ChannelVolumeMoved" => new ControllerEvent.ChannelVolumeMoved(Deck0(a), RequireValue(a)),
            "JogRotated" => new ControllerEvent.JogRotated(Deck0(a), a.Delta ?? 0, ParseJogSource(a)),
            "JogTouch" => new ControllerEvent.JogTouch(Deck0(a), a.On ?? true),
            "EqMoved" => new ControllerEvent.EqMoved(Deck0(a), ParseBand(a), RequireValue(a)),
            "FilterMoved" => new ControllerEvent.FilterMoved(Deck0(a), RequireValue(a)),
            "StemToggle" => new ControllerEvent.StemToggle(Deck0(a), a.Group ?? 0),
            "TempoMoved" => new ControllerEvent.TempoMoved(Deck0(a), RequireValue(a)),
            "BeatLoopToggle" => new ControllerEvent.BeatLoopToggle(Deck0(a), a.Bars ?? 4),
            "BeatLoopHalve" => new ControllerEvent.BeatLoopHalve(Deck0(a)),
            "BeatLoopDouble" => new ControllerEvent.BeatLoopDouble(Deck0(a)),
            "NudgeGrid" => new ControllerEvent.NudgeGrid(a.Deck is { } d ? d - 1 : -1, a.Delta ?? 1),
            "DeckShift" => new ControllerEvent.DeckShift(Deck0(a), a.On ?? true),
            "StemLevelMode" => new ControllerEvent.StemLevelMode(a.On ?? true),
            "BeatSyncPressed" => new ControllerEvent.BeatSyncPressed(Deck0(a)),
            "CycleTempoRange" => new ControllerEvent.CycleTempoRange(Deck0(a)),
            "PadPageSelected" => new ControllerEvent.PadPageSelected(Deck0(a), ParsePage(a)),
            "EchoToggle" => new ControllerEvent.EchoToggle(Deck0(a)),
            "TransportCuePressed" => new ControllerEvent.TransportCuePressed(Deck0(a), a.Shifted ?? false),
            "BrowseRotated" => new ControllerEvent.BrowseRotated(a.Delta ?? 1),
            "BrowsePressed" => new ControllerEvent.BrowsePressed(),
            "BrowseReleased" => new ControllerEvent.BrowseReleased(),
            _ => throw new FormatException($"gesture \"{evt}\": not a known ControllerEvent"),
        };
    }

    private int Deck0(ScenarioAction a) =>
        a.Deck is 1 or 2
            ? a.Deck.Value - 1
            : throw new FormatException($"gesture \"{a.Event}\" requires \"deck\": 1 or 2");

    private double RequireValue(ScenarioAction a) =>
        a.Value ?? throw new FormatException($"gesture \"{a.Event}\" requires \"value\"");

    private JogSource ParseJogSource(ScenarioAction a) => a.JogSource switch
    {
        "top" or null => JogSource.TopPlatter,
        "ring" => JogSource.SideRing,
        var s => throw new FormatException($"gesture \"JogRotated\": unknown \"jogSource\" \"{s}\" (expected \"top\" or \"ring\")"),
    };

    private EqBand ParseBand(ScenarioAction a) => a.Band switch
    {
        "Low" or null => EqBand.Low,
        "Mid" => EqBand.Mid,
        "High" => EqBand.High,
        var s => throw new FormatException($"gesture \"EqMoved\": unknown \"band\" \"{s}\" (expected \"Low\", \"Mid\" or \"High\")"),
    };

    private PadPage ParsePage(ScenarioAction a) => a.Page switch
    {
        "HotCue" or null => PadPage.HotCue,
        "PadFx1" => PadPage.PadFx1,
        var s => throw new FormatException($"gesture \"PadPageSelected\": unknown \"page\" \"{s}\" (expected \"HotCue\" or \"PadFx1\")"),
    };
}
