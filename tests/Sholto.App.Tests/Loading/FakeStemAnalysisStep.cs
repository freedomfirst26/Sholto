using System.Collections.Concurrent;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;

namespace Sholto.App.Tests;

/// <summary>A demucs stand-in that records every file it is asked to separate, optionally waits for the test or
/// throws, so a test can see whether and when demucs would have run.</summary>
internal sealed class FakeStemAnalysisStep : IStemAnalysisStep
{
    private readonly ConcurrentQueue<string> _runs = new();
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _gated;
    private readonly Exception? _failure;

    public FakeStemAnalysisStep(bool gated = false, Exception? failure = null)
    {
        _gated = gated;
        _failure = failure;
    }

    public string StepName => AnalysisSteps.Stems;
    public bool IsAvailable => true;
    public IReadOnlyCollection<string> Runs => _runs;

    public void Release() => _release.TrySetResult();

    public async Task<StemPaths> AnalyzeAsync(string filePath, IAnalysisReporter reporter, CancellationToken ct = default)
    {
        _runs.Enqueue(filePath);
        if (_gated) await _release.Task.WaitAsync(ct);
        if (_failure is not null) throw _failure;
        return new StemPaths("/stems" + filePath);
    }
}
