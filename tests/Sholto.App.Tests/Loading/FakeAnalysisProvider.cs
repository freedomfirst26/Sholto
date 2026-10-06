using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Always "analyses" to the BPM it was given.</summary>
internal sealed class FakeAnalysisProvider(double bpm) : IAnalysisProvider
{
    private readonly double _bpm = bpm;

    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        Task.FromResult(Make());

    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default) =>
        new BasicAnalysisRequest(_ => Task.FromResult(Make()));

    public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
        Task.FromResult(Make());

    private BasicAnalysis Make() => new(
        new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
        Bpm: _bpm,
        BeatTimes: [],
        DownbeatTimes: []);
}
