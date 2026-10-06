using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Audio;
using Sholto.App.Dsp;
using Sholto.Interface.Bench.Rendering;
using Sholto.Interface.Bench.Scenario;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Sholto.Interface.Bench.Tests.Rendering;

/// <summary>Builds Bench's real render stack the way <c>Program</c> composes it (real MP3/WAV decode,
/// the same router) and renders a one-deck "load, play, wait" scenario of a WAV file to a float WAV.</summary>
public sealed class BenchRenderRig
{
    private readonly OfflineRenderer _renderer;

    public BenchRenderRig()
    {
        var naudio = new NAudioDecoding();
        var decoder = new AudioFileDecoder([new Mp3DecodeStrategy(naudio), new WavDecodeStrategy(naudio)]);
        var factory = new BenchDeckFactory(
            decoder,
            new NoOpStemAnalysisStep(),
            new AnalysisProvider((_, _) => throw new NotSupportedException("not used")),
            Options.Create(new WaveformBandOptions()),
            new Sholto.Data.ImmediateAppThread());
        _renderer = new OfflineRenderer(new BenchDeck(factory), new EqualPowerCrossfade(), new CueOutputRouterFactory());
    }

    public RenderedWav Render(string sourceWavPath, double seconds, string outWavPath)
    {
        var scenario = new ScenarioFactory().Create(
            "{ \"actions\": [ { \"action\": \"load\", \"deck\": 1, \"track\": " + System.Text.Json.JsonSerializer.Serialize(sourceWavPath) +
            ", \"value\": 1.0 }, { \"action\": \"crossfader\", \"value\": 0.0 }, { \"action\": \"play\", \"deck\": 1 }, " +
            "{ \"action\": \"wait\", \"seconds\": " + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " } ] }");
        var format = new AudioFormat { SampleRate = AudioFileDecoder.TargetSampleRate, Channels = 2, Format = SampleFormat.F32 };
        _renderer.RenderScenario(scenario, format, outWavPath);
        return new RenderedWav(outWavPath);
    }
}
