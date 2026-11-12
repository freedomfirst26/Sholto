using System.Text.Json;
using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.Data;
using Sholto.Interface.Bench.Headless;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;

namespace Sholto.Interface.Bench.Tests.Headless;

public sealed class HeadlessObservationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"obs-{Guid.NewGuid():N}");

    public HeadlessObservationTests()
    {
        Directory.CreateDirectory(_dir);
        new ToneFixture(440, 1).WriteWav(Path.Combine(_dir, "a.wav"));
        new ToneFixture(660, 1).WriteWav(Path.Combine(_dir, "b.wav"));
    }

    public void Dispose() => Directory.Delete(_dir, true);

    private IHeadlessHostFactory NewFactory()
    {
        var naudio = new NAudioDecoding();
        var decoder = new AudioFileDecoder([new Mp3DecodeStrategy(naudio), new WavDecodeStrategy(naudio)]);
        var appThread = new ImmediateAppThread();
        var deckFactory = new BenchDeckFactory(
            decoder,
            new NoOpStemAnalysisStep(),
            new AnalysisProvider((_, _) => throw new NotSupportedException("not used")),
            Options.Create(new WaveformBandOptions()),
            appThread, new RecordingLogFactory());
        return new HeadlessHostFactory(new BenchDeck(deckFactory), deckFactory, decoder, new DeckAdvance(deckFactory), appThread, new EqualPowerCrossfade(), new RecordingLogFactory());
    }

    [Fact]
    public void RunScenario_ScanBrowseLoadEchoTempoRange_EachObservedKeyChanges()
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"scan\", \"dir\": " + JsonSerializer.Serialize(_dir) + " }, " +
            "{ \"action\": \"gesture\", \"event\": \"BrowseRotated\", \"delta\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"LoadToDeck\", \"deck\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"EchoHold\", \"deck\": 1, \"on\": true }, " +
            "{ \"action\": \"gesture\", \"event\": \"EchoHold\", \"deck\": 1, \"on\": false }, " +
            "{ \"action\": \"gesture\", \"event\": \"CycleTempoRange\", \"deck\": 1 } ] }");

        var session = NewFactory().Create().RunScenario(scenario);

        var scan = session.Driver.Outcomes[0].Changed;
        Assert.Equal(0, scan["Library.Rows"].Before);
        Assert.Equal(2, scan["Library.Rows"].After);

        var g = session.Driver.GestureOutcomes;
        Assert.Contains("Library.SelectedIndex", g[0].Changed.Keys);
        Assert.Contains("Deck1.IsLoaded", g[1].Changed.Keys);
        Assert.Equal(false, g[2].Changed["Deck1.EchoActive"].Before);
        Assert.Equal(true, g[2].Changed["Deck1.EchoActive"].After);
        Assert.Equal(true, g[3].Changed["Deck1.EchoActive"].Before);
        Assert.Equal(false, g[3].Changed["Deck1.EchoActive"].After);
        Assert.Contains("Deck1.TempoRange", g[4].Changed.Keys);
    }

    [Fact]
    public void Snapshot_ObservesLoopOnBothDecks()
    {
        var session = NewFactory().Create().Open();
        var snap = new CoreSnapshot().Take(session.Core, session.Driver.Decks);
        Assert.Contains("Deck1.Loop", snap.Keys);
        Assert.Contains("Deck2.Loop", snap.Keys);
        Assert.Null(snap["Deck1.Loop"]);
    }

    public static TheoryData<string> DeckFieldKeys() =>
    [
        "PlayState", "EffectiveBpm", "BpmMultiplier", "MarkerSecsCount", "SectionsCount",
        "DrumsActive", "VocalsActive", "InstrumentalActive", "DrumsLevel", "VocalsLevel", "InstrumentalLevel",
        "EditOpen", "IsScratching",
    ];

    [Theory]
    [MemberData(nameof(DeckFieldKeys))]
    public void Snapshot_ObservesDeckFieldOnBothDecks(string field)
    {
        var session = NewFactory().Create().Open();
        var snap = new CoreSnapshot().Take(session.Core, session.Driver.Decks);
        Assert.Contains($"Deck1.{field}", snap.Keys);
        Assert.Contains($"Deck2.{field}", snap.Keys);
    }

    [Fact]
    public void Snapshot_FreshDecks_ReportStoppedUnityAndAllStemsOn()
    {
        var session = NewFactory().Create().Open();
        var snap = new CoreSnapshot().Take(session.Core, session.Driver.Decks);
        Assert.Equal(PlayPhase.Stopped, snap["Deck2.PlayState"]);
        Assert.Equal(1.0, snap["Deck2.BpmMultiplier"]);
        Assert.Equal(0, snap["Deck2.MarkerSecsCount"]);
        Assert.Equal(0, snap["Deck2.SectionsCount"]);
        Assert.Equal(true, snap["Deck2.DrumsActive"]);
        Assert.Equal(true, snap["Deck2.VocalsActive"]);
        Assert.Equal(true, snap["Deck2.InstrumentalActive"]);
        Assert.Equal(false, snap["Deck2.EditOpen"]);
        Assert.Equal(false, snap["Deck2.IsScratching"]);
    }

    [Fact]
    public void RunScenario_StemTogglesAndPause_ReportStemAndPlayStateBeforeAndAfter()
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + JsonSerializer.Serialize(Path.Combine(_dir, "a.wav")) + ", \"value\": 1.0 }, " +
            "{ \"action\": \"wait\", \"seconds\": 0.2 }, " +
            "{ \"action\": \"gesture\", \"event\": \"StemToggle\", \"deck\": 1, \"group\": 0 }, " +
            "{ \"action\": \"gesture\", \"event\": \"StemToggle\", \"deck\": 1, \"group\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"StemToggle\", \"deck\": 1, \"group\": 0 }, " +
            "{ \"action\": \"gesture\", \"event\": \"PlayPressed\", \"deck\": 1 } ] }");

        var session = NewFactory().Create().RunScenario(scenario);

        var g = session.Driver.GestureOutcomes;
        Assert.Equal(4, g.Count);
        Assert.Equal(true, g[0].Changed["Deck1.DrumsActive"].Before);
        Assert.Equal(false, g[0].Changed["Deck1.DrumsActive"].After);
        Assert.DoesNotContain("Deck1.VocalsActive", g[0].Changed.Keys);
        Assert.Equal(true, g[1].Changed["Deck1.VocalsActive"].Before);
        Assert.Equal(false, g[1].Changed["Deck1.VocalsActive"].After);
        Assert.Equal(false, g[2].Changed["Deck1.DrumsActive"].Before);
        Assert.Equal(true, g[2].Changed["Deck1.DrumsActive"].After);
        Assert.Equal(PlayPhase.Playing, g[3].Changed["Deck1.PlayState"].Before);
        Assert.Equal(PlayPhase.Stopped, g[3].Changed["Deck1.PlayState"].After);
        Assert.All(g, o => Assert.DoesNotContain("Deck2.DrumsActive", o.Changed.Keys));
    }
}
