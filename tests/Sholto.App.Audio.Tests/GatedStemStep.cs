using System.Collections.Concurrent;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Audio.Tests;

/// <summary>A demucs stand-in whose run per file waits for the test, recording how many runs were ever in flight at once.</summary>
internal sealed class GatedStemStep : IStemAnalysisStep
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();
    private int _inFlight;
    private int _maxConcurrent;

    public string StepName => "stems";
    public bool IsAvailable => true;
    public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

    public bool Started(string path) => _gates.ContainsKey(path);

    public void Release(string path) => Gate(path).SetResult();

    public async Task<StemPaths> AnalyzeAsync(string filePath, IAnalysisReporter reporter, CancellationToken ct = default)
    {
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while ((seen = Volatile.Read(ref _maxConcurrent)) < now &&
               Interlocked.CompareExchange(ref _maxConcurrent, now, seen) != seen) { }
        try { await Gate(filePath).Task; }
        finally { Interlocked.Decrement(ref _inFlight); }
        return new StemPaths("/stems" + filePath);
    }

    private TaskCompletionSource Gate(string path) =>
        _gates.GetOrAdd(path, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
