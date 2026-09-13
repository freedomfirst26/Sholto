using Sholto.Bench.Scenario;

namespace Sholto.Bench.Tests;

public sealed class ScenarioParserTests
{
    [Fact]
    public void Parse_ValidScenario_ParsesEveryActionKind()
    {
        var scenario = ScenarioParser.Parse("""
            {
              "actions": [
                { "action": "load", "deck": 1, "track": "/tmp/a.mp3", "value": 0.8 },
                { "action": "gain", "deck": 1, "value": 0.5 },
                { "action": "play", "deck": 1 },
                { "action": "crossfader", "value": 0.25 },
                { "action": "wait", "seconds": 2.0 },
                { "action": "wait", "beats": 4, "bpm": 128 },
                { "action": "key", "key": "Space" },
                { "action": "click", "target": "TrackList", "index": 0 },
                { "action": "screenshot", "out": "/tmp/shot.png" },
                { "action": "gesture", "event": "JogRotated", "deck": 1, "delta": 5, "jogSource": "ring" },
                { "action": "midi", "midiChannel": 1, "note": 11, "velocity": 127 },
                { "action": "midi", "midiChannel": 7, "cc": 34, "ccValue": 64 }
              ]
            }
            """);

        Assert.Equal(12, scenario.Actions.Count);
        Assert.Equal("load", scenario.Actions[0].Action);
        Assert.Equal(1, scenario.Actions[0].Deck);
        Assert.Equal("/tmp/a.mp3", scenario.Actions[0].Track);
    }

    [Theory]
    [InlineData("""{ "actions": [ { "action": "load", "track": "/tmp/a.mp3" } ] }""")]         // missing deck
    [InlineData("""{ "actions": [ { "action": "load", "deck": 1 } ] }""")]                     // missing track
    [InlineData("""{ "actions": [ { "action": "load", "deck": 3, "track": "/tmp/a.mp3" } ] }""")] // deck out of range
    [InlineData("""{ "actions": [ { "action": "gain", "deck": 1 } ] }""")]                     // missing value
    [InlineData("""{ "actions": [ { "action": "gain", "deck": 1, "value": 1.5 } ] }""")]       // value out of range
    [InlineData("""{ "actions": [ { "action": "play" } ] }""")]                                // missing deck
    [InlineData("""{ "actions": [ { "action": "crossfader" } ] }""")]                          // missing value
    [InlineData("""{ "actions": [ { "action": "wait" } ] }""")]                                // neither seconds nor beats
    [InlineData("""{ "actions": [ { "action": "wait", "seconds": 1, "beats": 4, "bpm": 120 } ] }""")] // both
    [InlineData("""{ "actions": [ { "action": "wait", "beats": 4 } ] }""")]                    // beats without bpm
    [InlineData("""{ "actions": [ { "action": "key" } ] }""")]                                 // missing key
    [InlineData("""{ "actions": [ { "action": "click" } ] }""")]                               // missing target
    [InlineData("""{ "actions": [ { "action": "screenshot" } ] }""")]                          // missing out
    [InlineData("""{ "actions": [ { "action": "levitate" } ] }""")]                            // unknown action
    [InlineData("""{ "actions": [ { "action": "gesture" } ] }""")]                             // missing event
    [InlineData("""{ "actions": [ { "action": "midi", "midiChannel": 1 } ] }""")]              // missing note/cc
    [InlineData("""{ "actions": [ { "action": "midi", "midiChannel": 1, "note": 11, "cc": 34 } ] }""")] // both note and cc
    [InlineData("""{ "actions": [ { "action": "midi", "note": 11 } ] }""")]                    // missing midiChannel
    [InlineData("""{ "actions": [ { "action": "midi", "midiChannel": 1, "cc": 34 } ] }""")]    // cc without ccValue
    public void Parse_RejectsInvalidAction_BeforeAnyActionWouldRun(string json)
    {
        Assert.Throws<FormatException>(() => ScenarioParser.Parse(json));
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsFormatExceptionNotJsonException()
    {
        Assert.Throws<FormatException>(() => ScenarioParser.Parse("{ not json"));
    }

    [Fact]
    public void Parse_SecondActionInvalid_StillFails_NotJustTheFirst()
    {
        // Regression guard: validation must walk every action, not stop after
        // the first one parses — a typo three steps in should fail up front.
        var ex = Assert.Throws<FormatException>(() => ScenarioParser.Parse("""
            {
              "actions": [
                { "action": "play", "deck": 1 },
                { "action": "play", "deck": 9 }
              ]
            }
            """));
        Assert.Contains("scenario action 1", ex.Message);
    }
}
