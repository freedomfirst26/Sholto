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
            appThread);
        return new HeadlessHostFactory(new BenchDeck(deckFactory), deckFactory, decoder, new DeckAdvance(deckFactory), appThread, new EqualPowerCrossfade());
    }

    [Fact]
    public void RunScenario_ScanBrowseLoadEchoTempoRange_EachObservedKeyChanges()
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"scan\", \"dir\": " + JsonSerializer.Serialize(_dir) + " }, " +
            "{ \"action\": \"gesture\", \"event\": \"BrowseRotated\", \"delta\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"LoadToDeck\", \"deck\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"EchoToggle\", \"deck\": 1 }, " +
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
        Assert.Contains("Deck1.TempoRange", g[3].Changed.Keys);
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
}
