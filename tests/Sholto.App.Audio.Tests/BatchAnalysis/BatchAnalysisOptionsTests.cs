using Sholto.App.Audio.BatchAnalysis;

namespace Sholto.App.Audio.Tests.BatchAnalysis;

public class BatchAnalysisOptionsTests
{
    [Fact]
    public void Default_runs_two_tracks_at_once() =>
        Assert.Equal(2, new BatchAnalysisOptions().MaxParallelTracks);
}
