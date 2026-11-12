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

/// <summary>A <see cref="HeadlessSession"/> owns its decks: disposing it unloads both, closing the loaded
/// track's streaming source file. Probed with <see cref="FileShare.None"/>, which .NET on Linux backs with
/// an exclusive flock, so a still-open deck stream makes the probe throw there as well as on Windows.</summary>
public sealed class HeadlessSessionDisposeTests : IDisposable
{
    private readonly string _wav = Path.Combine(Path.GetTempPath(), $"dispose-{Guid.NewGuid():N}.wav");

    public HeadlessSessionDisposeTests() => new ToneFixture(440, 2).WriteWav(_wav);

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
            appThread, new RecordingLogFactory());
        return new HeadlessHostFactory(new BenchDeck(deckFactory), deckFactory, decoder, new DeckAdvance(deckFactory), appThread, new EqualPowerCrossfade(), new RecordingLogFactory());
    }

    private HeadlessSession RunLoadScenario() =>
        NewFactory().Create().RunScenario(new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + JsonSerializer.Serialize(_wav) + ", \"value\": 1.0 }, " +
            "{ \"action\": \"wait\", \"seconds\": 0.2 } ] }"));

    private void OpenExclusive()
    {
        using var probe = File.Open(_wav, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    [Fact]
    public void Undisposed_session_holds_the_loaded_track_open()
    {
        using var session = RunLoadScenario();

        Assert.ThrowsAny<IOException>(OpenExclusive);
    }

    [Fact]
    public void Dispose_closes_the_loaded_track()
    {
        var session = RunLoadScenario();

        session.Dispose();

        OpenExclusive();
    }

    [Fact]
    public void A_second_dispose_is_harmless()
    {
        var session = RunLoadScenario();

        session.Dispose();
        session.Dispose();

        OpenExclusive();
    }
}
