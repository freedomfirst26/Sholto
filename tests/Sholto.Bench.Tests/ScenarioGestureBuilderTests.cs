using Sholto.Bench.Controller;
using Sholto.Bench.Scenario;
using ControllerEvent = Sholto.Controller.ControllerEvent;
using EqBand = Sholto.Controller.EqBand;
using JogSource = Sholto.Controller.JogSource;
using PadPage = Sholto.Controller.PadPage;

namespace Sholto.Bench.Tests;

/// <summary>
/// <see cref="ScenarioGestureBuilder"/> is the translation the "gesture" scenario
/// action rests on: get a field name wrong here and a scenario silently drives
/// the wrong deck or the wrong control, with no compiler to catch it. These pin
/// the mapping for the events a scenario is most likely to script, plus the
/// deliberate rejection of the two raw pre-Controller presses.
/// </summary>
public sealed class ScenarioGestureBuilderTests
{
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
            ScenarioGestureBuilder.Build(Gesture("JogRotated", deck: 1, delta: 12)));
        Assert.Equal(0, evt.Deck);
        Assert.Equal(12, evt.Delta);
        Assert.Equal(JogSource.TopPlatter, evt.Source);
    }

    [Fact]
    public void JogRotated_RingMapsToSideRing()
    {
        var evt = Assert.IsType<ControllerEvent.JogRotated>(
            ScenarioGestureBuilder.Build(Gesture("JogRotated", deck: 2, delta: -3, jogSource: "ring")));
        Assert.Equal(1, evt.Deck);
        Assert.Equal(JogSource.SideRing, evt.Source);
    }

    [Fact]
    public void PlayPressed_Deck2_MapsToZeroIndexedDeckOne()
    {
        var evt = Assert.IsType<ControllerEvent.PlayPressed>(
            ScenarioGestureBuilder.Build(Gesture("PlayPressed", deck: 2)));
        Assert.Equal(1, evt.Deck);
    }

    [Fact]
    public void CueChanged_CarriesOnFlag()
    {
        var evt = Assert.IsType<ControllerEvent.CueChanged>(
            ScenarioGestureBuilder.Build(Gesture("CueChanged", deck: 1, on: true)));
        Assert.Equal(0, evt.Deck);
        Assert.True(evt.On);
    }

    [Fact]
    public void EqMoved_ParsesBand()
    {
        var evt = Assert.IsType<ControllerEvent.EqMoved>(
            ScenarioGestureBuilder.Build(Gesture("EqMoved", deck: 1, value: 0.75, band: "High")));
        Assert.Equal(EqBand.High, evt.Band);
        Assert.Equal(0.75, evt.Value);
    }

    [Fact]
    public void PadPageSelected_ParsesPage()
    {
        var evt = Assert.IsType<ControllerEvent.PadPageSelected>(
            ScenarioGestureBuilder.Build(Gesture("PadPageSelected", deck: 1, page: "PadFx1")));
        Assert.Equal(PadPage.PadFx1, evt.Page);
    }

    [Fact]
    public void NudgeGrid_OmittedDeck_MeansAnyDeck()
    {
        var evt = Assert.IsType<ControllerEvent.NudgeGrid>(
            ScenarioGestureBuilder.Build(Gesture("NudgeGrid", delta: -1)));
        Assert.Equal(-1, evt.Deck);
        Assert.Equal(-1, evt.Beats);
    }

    [Fact]
    public void BrowseRotated_NeedsNoDeck()
    {
        var evt = Assert.IsType<ControllerEvent.BrowseRotated>(
            ScenarioGestureBuilder.Build(Gesture("BrowseRotated", delta: 4)));
        Assert.Equal(4, evt.Delta);
    }

    [Theory]
    [InlineData("CueToggle")]
    [InlineData("MasterCuePressed")]
    public void RawPreControllerPresses_AreRejected_WithAPointerToTheHighLevelEvent(string evt)
    {
        var ex = Assert.Throws<FormatException>(() => ScenarioGestureBuilder.Build(Gesture(evt, deck: 1)));
        Assert.Contains("never reaches the recognizer", ex.Message);
    }

    [Fact]
    public void UnknownEventName_Throws()
    {
        Assert.Throws<FormatException>(() => ScenarioGestureBuilder.Build(Gesture("NotARealEvent")));
    }

    [Fact]
    public void MissingDeck_OnAPerDeckEvent_ThrowsRatherThanDefaulting()
    {
        Assert.Throws<FormatException>(() => ScenarioGestureBuilder.Build(Gesture("PlayPressed")));
    }
}
