using Microsoft.Extensions.Options;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers.Vocals;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Stems;
using Sholto.Data;

namespace Sholto.App.Audio.Tests;

public class StemAnalysisStageGateTests
{
    [Theory]
    [InlineData(StemDevice.Cuda, true)]
    [InlineData(StemDevice.Cpu, false)]
    public async Task Overlap_is_allowed_only_on_cuda(StemDevice device, bool expected)
    {
        var stage = Stage(new GatedStemStep(), device);

        Assert.Equal(expected, await stage.CanOverlapBasicAnalysisAsync());
    }

    [Fact]
    public async Task A_second_decks_demucs_waits_for_the_first_to_finish()
    {
        var step = new GatedStemStep();
        var gate = new SemaphoreStemGate();
        var deckA = Stage(step, StemDevice.Cuda, gate);
        var deckB = Stage(step, StemDevice.Cuda, gate);

        var runA = Task.Run(() => deckA.RunAsync(Track("/a.mp3")));
        Assert.True(SpinWait.SpinUntil(() => step.Started("/a.mp3"), 2000));
        var runB = Task.Run(() => deckB.RunAsync(Track("/b.mp3")));
        await Task.Delay(300);
        Assert.False(step.Started("/b.mp3"));

        step.Release("/a.mp3");
        Assert.True(SpinWait.SpinUntil(() => step.Started("/b.mp3"), 2000));
        Assert.Equal(1, step.MaxConcurrent);

        step.Release("/b.mp3");
        await Task.WhenAll(runA, runB);
    }

    [Fact]
    public async Task A_cancelled_wait_for_the_gate_does_not_start_demucs()
    {
        var step = new GatedStemStep();
        var gate = new SemaphoreStemGate();
        var deckA = Stage(step, StemDevice.Cuda, gate);
        var deckB = Stage(step, StemDevice.Cuda, gate);
        var runA = Task.Run(() => deckA.RunAsync(Track("/a.mp3")));
        Assert.True(SpinWait.SpinUntil(() => step.Started("/a.mp3"), 2000));

        using var cts = new CancellationTokenSource();
        var runB = Task.Run(() => deckB.RunAsync(Track("/b.mp3"), cts.Token));
        await Task.Delay(100);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runB);
        Assert.False(step.Started("/b.mp3"));
        step.Release("/a.mp3");
        await runA;
    }

    private static DecodedTrack Track(string path) => new(path, new float[8], 48000, 2);

    private static StemAnalysisStage Stage(IStemAnalysisStep step, StemDevice device, IStemGate? gate = null) =>
        new(step, new SilentStemDecoder(),
            new WaveformPeakAnalyzer(
                new WaveformBandSplitterFactory(new BiquadFactory(), Options.Create(new WaveformBandOptions())),
                new WaveformPeaksFactory()),
            new VocalRegionAnalyzer(), new AnalysisReporter(Array.Empty<string>()),
            new FixedStemDevice(device), gate ?? new SemaphoreStemGate());

    private sealed class SilentStemDecoder : IStemDecoder
    {
        public Task<StemSamples> DecodeAsync(StemPaths paths, CancellationToken ct = default) =>
            Task.FromResult(new StemSamples([], [], [], []));
    }
}
