using System.Globalization;
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
using Sholto.Interface.Bench.Tests.Rendering;

namespace Sholto.Interface.Bench.Tests.Headless;

/// <summary>EQ and filter asserted through the controller path: gesture -> bus -> command handlers -> deck,
/// heard in the WAV the headless host renders.</summary>
public sealed class HeadlessAudioTests : IDisposable
{
    private readonly string _tone = Path.Combine(Path.GetTempPath(), $"sine60-{Guid.NewGuid():N}.wav");
    private readonly string _out = Path.Combine(Path.GetTempPath(), $"headless-{Guid.NewGuid():N}.wav");

    public HeadlessAudioTests() => new ToneFixture(60, 3).WriteWav(_tone);

    public void Dispose()
    {
        File.Delete(_tone);
        File.Delete(_out);
    }

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

    private double SecondDropDb(string gestureJson)
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + JsonSerializer.Serialize(_tone) + ", \"value\": 1.0 }, " +
            "{ \"action\": \"wait\", \"seconds\": 1 }, " + gestureJson + ", { \"action\": \"wait\", \"seconds\": 1 } ] }");

        NewFactory().Create().RunScenario(scenario, _out);

        var wav = new RenderedWav(_out);
        Assert.Equal(48000, wav.SampleRate);
        Assert.Equal(96000, wav.Left.Length);
        double first = Rms(wav.Left, 0, 48000);
        double second = Rms(wav.Left, 48000, 48000);
        Assert.True(first > 0.01, $"first second is silent (rms {first})");
        return 20 * Math.Log10(first / Math.Max(second, 1e-12));
    }

    private static double Rms(float[] x, int start, int count)
    {
        double sum = 0;
        for (int i = start; i < start + count; i++) sum += (double)x[i] * x[i];
        return Math.Sqrt(sum / count);
    }

    [Fact]
    public void RunScenario_EqLowKilled_SecondSecondAtLeast12DbBelowFirst()
    {
        double drop = SecondDropDb("{ \"action\": \"gesture\", \"event\": \"EqMoved\", \"deck\": 1, \"band\": \"Low\", \"value\": 0.0 }");
        Assert.True(drop >= 12, $"EQ low kill dropped only {drop.ToString("F1", CultureInfo.InvariantCulture)} dB");
    }

    [Fact]
    public void RunScenario_FilterHighPass_SecondSecondAtLeast12DbBelowFirst()
    {
        double drop = SecondDropDb("{ \"action\": \"gesture\", \"event\": \"FilterMoved\", \"deck\": 1, \"value\": 1.0 }");
        Assert.True(drop >= 12, $"filter high-pass dropped only {drop.ToString("F1", CultureInfo.InvariantCulture)} dB");
    }
}
