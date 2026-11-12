using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

/// <summary>A synchronous, thread-safe <see cref="IProgress{T}"/> that keeps every report in order.</summary>
internal sealed class ProgressLog : IProgress<BatchTrackProgress>
{
    private readonly List<BatchTrackProgress> _reports = [];

    public void Report(BatchTrackProgress value) { lock (_reports) _reports.Add(value); }

    public IReadOnlyList<BatchTrackProgress> For(string path)
    {
        lock (_reports) return _reports.Where(r => r.Path == path).ToList();
    }

    public IReadOnlyList<BatchTrackState> StatesFor(string path) => For(path).Select(r => r.State).ToList();
}
