using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Scenario;
using ControllerEvent = Sholto.Interface.Controller.ControllerEvent;
using EqBand = Sholto.Interface.Controller.EqBand;
using JogSource = Sholto.Interface.Controller.JogSource;
using PadPage = Sholto.Data.PadPage;

namespace Sholto.Interface.Bench.Tests;

/// <summary>
/// <see cref="ScenarioGestureFactory"/> is the translation the "gesture" scenario
/// action rests on: get a field name wrong here and a scenario silently drives
/// the wrong deck or the wrong control, with no compiler to catch it. These pin
/// the mapping for the events a scenario is most likely to script, plus the
/// deliberate rejection of the two raw pre-Controller presses.
/// </summary>
public sealed class ScenarioGestureFactoryTests
{
    private readonly IScenarioGestureFactory _factory = new ScenarioGestureFactory();

    private static ScenarioAction Gesture(string evt, int? deck = null, double? value = null,
        int? delta = null, string? jogSource = null, bool? on = null, int? group = null,
        string? band = null, int? bars = null, bool? shifted = null, string? page = null) =>
        new()
        {
            Action = "gesture",
            Event = evt,
            Deck = deck,
            Value = value,
            Delta = delta,
            JogSource = jogSource,
            On = on,
            Group = group,
            Band = band,
            Bars = bars,
            Shifted = shifted,
            Page = page,
        };

    [Fact]
    public void JogRotated_ConvertsOneIndexedDeckAndDefaultsToTopPlatter()
    {
        var evt = Assert.IsType<ControllerEvent.JogRotated>(
            _factory.Create(Gesture("JogRotated", deck: 1, delta: 12)));
        Assert.Equal(0, evt.Deck);
        Assert.Equal(12, evt.Delta);
        Assert.Equal(JogSource.TopPlatter, evt.Source);
    }

    [Fact]
    public void JogRotated_RingMapsToSideRing()
    {
        var evt = Assert.IsType<ControllerEvent.JogRotated>(
            _factory.Create(Gesture("JogRotated", deck: 2, delta: -3, jogSource: "ring")));
        Assert.Equal(1, evt.Deck);
        Assert.Equal(JogSource.SideRing, evt.Source);
    }

    [Fact]
    public void PlayPressed_Deck2_MapsToZeroIndexedDeckOne()
    {
        var evt = Assert.IsType<ControllerEvent.PlayPressed>(
            _factory.Create(Gesture("PlayPressed", deck: 2)));
        Assert.Equal(1, evt.Deck);
    }

    [Fact]
    public void CueToggle_Deck1_MapsToZeroIndexedDeckZero()
    {
        var evt = Assert.IsType<ControllerEvent.CueToggle>(
            _factory.Create(Gesture("CueToggle", deck: 1)));
        Assert.Equal(0, evt.Deck);
        Assert.IsType<ControllerEvent.MasterCuePressed>(_factory.Create(Gesture("MasterCuePressed")));
    }

    [Fact]
    public void EqMoved_ParsesBand()
    {
        var evt = Assert.IsType<ControllerEvent.EqMoved>(
            _factory.Create(Gesture("EqMoved", deck: 1, value: 0.75, band: "High")));
        Assert.Equal(EqBand.High, evt.Band);
        Assert.Equal(0.75, evt.Value);
    }

    [Fact]
    public void PadPageSelected_ParsesPage()
    {
        var evt = Assert.IsType<ControllerEvent.PadPageSelected>(
            _factory.Create(Gesture("PadPageSelected", deck: 1, page: "PadFx1")));
        Assert.Equal(PadPage.PadFx1, evt.Page);
    }

    [Fact]
    public void NudgeGrid_OmittedDeck_MeansAnyDeck()
    {
        var evt = Assert.IsType<ControllerEvent.NudgeGrid>(
            _factory.Create(Gesture("NudgeGrid", delta: -1)));
        Assert.Equal(-1, evt.Deck);
        Assert.Equal(-1, evt.Beats);
    }

    [Fact]
    public void BrowseRotated_NeedsNoDeck()
    {
        var evt = Assert.IsType<ControllerEvent.BrowseRotated>(
            _factory.Create(Gesture("BrowseRotated", delta: 4)));
        Assert.Equal(4, evt.Delta);
    }

    [Fact]
    public void UnknownEventName_Throws()
    {
        Assert.Throws<FormatException>(() => _factory.Create(Gesture("NotARealEvent")));
    }

    [Fact]
    public void MissingDeck_OnAPerDeckEvent_ThrowsRatherThanDefaulting()
    {
        Assert.Throws<FormatException>(() => _factory.Create(Gesture("PlayPressed")));
    }
}
