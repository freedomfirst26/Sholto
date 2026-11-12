using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Tests.Library;

/// <summary>Takes the paths one at a time, the way the real analyser takes no new track once the token is
/// cancelled: each track reports Started, then waits for <see cref="Release"/> before it reports Finished.</summary>
internal sealed class GatedBatchAnalyzer : IBatchAnalyzer
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Set once the first track has started and is waiting on the gate.</summary>
    public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Runs { get; private set; }

    public List<string> Started { get; } = [];

    public void Release() => _gate.TrySetResult();

    public async Task RunAsync(IReadOnlyList<string> paths, IProgress<BatchTrackProgress> progress, CancellationToken ct)
    {
        Runs++;
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            lock (Started) Started.Add(path);
            progress.Report(new BatchTrackProgress(path, BatchTrackState.Started));
            FirstStarted.TrySetResult();
            await _gate.Task;
            progress.Report(new BatchTrackProgress(path, BatchTrackState.Finished));
        }
    }
}
