using System.Collections.Concurrent;
using Sholto.App.Analysis;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stages;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio.Tests;

/// <summary>One fake for the three analysis ports <see cref="TrackAnalysisRun"/> calls (basic, key, stems).
/// Each result is a gate per file path: a call waits until the test completes it. The fake records the
/// <see cref="CancellationToken"/> it was handed but never honours it, so a test can tell a run that was
/// cancelled from a result that was merely discarded.</summary>
internal sealed class GatedAnalysisPipeline : IAnalysisProvider, IKeyAnalyzer, IStemStage
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BasicAnalysis>> _basic = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<KeyAnalysis>> _key = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<StemAnalysis>> _stems = new();
    private readonly ConcurrentDictionary<string, CancellationToken> _stemTokens = new();
    private readonly ConcurrentDictionary<string, CancellationToken> _begun = new();
    private readonly ConcurrentDictionary<string, int> _beginCounts = new();
    private readonly ConcurrentDictionary<string, int> _stemCounts = new();

    public bool IsAvailable => true;

    /// <summary>What <see cref="CanOverlapBasicAnalysisAsync"/> answers: true stands for demucs confirmed on CUDA.</summary>
    public bool CanOverlap { get; set; }

    public Task<bool> CanOverlapBasicAnalysisAsync(CancellationToken ct = default) => Task.FromResult(CanOverlap);

    public bool BasicRequested(string path) => _basic.ContainsKey(path);

    /// <summary>Whether the path-only phase of basic analysis (<see cref="Begin"/>) was started for <paramref name="path"/>.</summary>
    public bool BeginRequested(string path) => _begun.ContainsKey(path);

    /// <summary>The token <see cref="Begin"/> was handed for <paramref name="path"/>.</summary>
    public CancellationToken BeginToken(string path) => _begun[path];

    /// <summary>How many times <see cref="Begin"/> ran for <paramref name="path"/>.</summary>
    public int BeginCount(string path) => _beginCounts.GetValueOrDefault(path);

    /// <summary>How many times the stem stage ran for <paramref name="path"/>.</summary>
    public int StemsCount(string path) => _stemCounts.GetValueOrDefault(path);

    public bool StemsRequested(string path) => _stemTokens.ContainsKey(path);

    /// <summary>The token the stem stage was handed for <paramref name="path"/>.</summary>
    public CancellationToken StemToken(string path) => _stemTokens[path];

    /// <summary>Release the basic and key gates for <paramref name="path"/>; stems stay gated.</summary>
    public void CompleteBasicAndKey(string path, double bpm)
    {
        Gate(_basic, path).SetResult(new BasicAnalysis(null!, bpm, [0.0, 0.5], [0.0]));
        Gate(_key, path).SetResult(new KeyAnalysis(new Sholto.App.Analysis.Harmony.Key(0, true)));
    }

    /// <summary>Release only the basic gate for <paramref name="path"/>.</summary>
    public void CompleteBasic(string path, double bpm) =>
        Gate(_basic, path).SetResult(new BasicAnalysis(null!, bpm, [0.0, 0.5], [0.0]));

    /// <summary>Release only the key gate for <paramref name="path"/>.</summary>
    public void CompleteKey(string path) =>
        Gate(_key, path).SetResult(new KeyAnalysis(new Sholto.App.Analysis.Harmony.Key(0, true)));

    /// <summary>Release every gate for <paramref name="path"/> with results that name the track.</summary>
    public void CompleteAll(string path, double bpm)
    {
        CompleteBasicAndKey(path, bpm);
        Gate(_stems, path).SetResult(new StemAnalysis(
            new StemPaths("/stems" + path), new StemSamples([], [], [], []), []));
    }

    public Task<BasicAnalysis> GetAsync(DecodedTrack track, CancellationToken ct = default) =>
        Gate(_basic, track.FilePath).Task;

    public IBasicAnalysisRequest Begin(string filePath, CancellationToken ct = default)
    {
        _begun[filePath] = ct;
        _beginCounts.AddOrUpdate(filePath, 1, (_, n) => n + 1);
        return new BasicAnalysisRequest(track => Gate(_basic, track.FilePath).Task);
    }

    public Task<BasicAnalysis> RecomputeAsync(DecodedTrack track, CancellationToken ct = default) =>
        GetAsync(track, ct);

    public Task<KeyAnalysis> AnalyzeAsync(DecodedTrack track, IAnalysisReporter reporter, CancellationToken ct = default) =>
        Gate(_key, track.FilePath).Task;

    public Task<StemAnalysis> RunAsync(DecodedTrack track, CancellationToken ct = default) =>
        RunAsync(track.FilePath, track.SampleRate, track.Channels, ct);

    public Task<StemAnalysis> RunAsync(string filePath, int sampleRate, int channels, CancellationToken ct = default)
    {
        _stemTokens[filePath] = ct;
        _stemCounts.AddOrUpdate(filePath, 1, (_, n) => n + 1);
        return Gate(_stems, filePath).Task;
    }

    private static TaskCompletionSource<T> Gate<T>(
        ConcurrentDictionary<string, TaskCompletionSource<T>> gates, string path) =>
        gates.GetOrAdd(path, _ => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously));
}
