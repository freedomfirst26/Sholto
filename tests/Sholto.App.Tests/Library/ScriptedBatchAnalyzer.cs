using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Tests.Library;

/// <summary>Records the paths it was asked for and reports, synchronously and in order, the state the
/// script gives each path (Finished when the script does not name it).</summary>
internal sealed class ScriptedBatchAnalyzer(IReadOnlyDictionary<string, BatchTrackState> script) : IBatchAnalyzer
{
    private readonly IReadOnlyDictionary<string, BatchTrackState> _script = script;

    public List<string> Paths { get; } = [];

    public Task RunAsync(IReadOnlyList<string> paths, IProgress<BatchTrackProgress> progress, CancellationToken ct)
    {
        Paths.AddRange(paths);
        foreach (var path in paths)
        {
            progress.Report(new BatchTrackProgress(path, BatchTrackState.Started));
            var state = _script.GetValueOrDefault(path, BatchTrackState.Finished);
            progress.Report(new BatchTrackProgress(path, state, state == BatchTrackState.Failed ? "boom" : null));
        }
        return Task.CompletedTask;
    }
}
