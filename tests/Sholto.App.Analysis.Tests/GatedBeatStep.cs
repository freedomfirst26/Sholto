using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Processing;

namespace Sholto.App.Analysis.Tests;

/// <summary>A beat step that records when it was asked and then waits for the test, standing in for madmom.</summary>
internal sealed class GatedBeatStep : IBeatAnalysisStep
{
    private readonly TaskCompletionSource<DetectedBeats> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    public string StepName => "beats";
    public bool IsAvailable => true;
    public bool Started => Volatile.Read(ref _started) == 1;

    public void Complete() => _result.SetResult(new DetectedBeats(120, [0.0, 0.5, 1.0, 1.5], [0.0]));

    public Task<DetectedBeats> AnalyzeAsync(string filePath, CancellationToken ct = default)
    {
        Volatile.Write(ref _started, 1);
        return _result.Task;
    }
}
