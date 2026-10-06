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

public sealed class HeadlessScenarioTests : IDisposable
{
    private readonly string _wav = Path.Combine(Path.GetTempPath(), $"tone48k-{Guid.NewGuid():N}.wav");

    public HeadlessScenarioTests() => new ToneFixture(440, 2).WriteWav(_wav);

    public void Dispose() => File.Delete(_wav);

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
    public void RunScenario_LoadWaitPlayCue_WaitDoesNotThrowAndGesturesChangeState()
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + JsonSerializer.Serialize(_wav) + ", \"value\": 1.0 }, " +
            "{ \"action\": \"wait\", \"seconds\": 0.5 }, " +
            "{ \"action\": \"gesture\", \"event\": \"PlayPressed\", \"deck\": 1 }, " +
            "{ \"action\": \"gesture\", \"event\": \"CueToggle\", \"deck\": 1 } ] }");

        var session = NewFactory().Create().RunScenario(scenario);

        var outcomes = session.Driver.GestureOutcomes;
        Assert.Equal(2, outcomes.Count);
        Assert.Equal(true, outcomes[0].Changed["Deck1.IsPlaying"].Before);
        Assert.Equal(false, outcomes[0].Changed["Deck1.IsPlaying"].After);
        Assert.Equal(false, outcomes[1].Changed["Deck1.CueActive"].Before);
        Assert.Equal(true, outcomes[1].Changed["Deck1.CueActive"].After);
        Assert.True(session.Core.Decks.Deck1.Playhead.PositionFrames > 0);
    }
}
