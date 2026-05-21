using Sholto.Analysis;

namespace Sholto.App.Tests;

public class BeatgridTests
{
    private const double Epsilon = 0.005; // 5 ms tolerance

    [Fact]
    public void Synthesize_ProducesEquidistantGrid()
    {
        // 120 BPM, 4/4 → bar period = 2.0 s. Anchor at 0.5 s.
        double bpm = 120.0;
        double period = 2.0;
        var beats = Enumerable.Range(0, 240).Select(i => 0.5 + i * 0.5).ToArray();
        var downbeats = Enumerable.Range(0, 60).Select(i => 0.5 + i * period).ToArray();

        var grid = Beatgrid.Synthesize(bpm, beats, downbeats, durationSec: 120.0);

        Assert.True(grid.Length > 50);
        // All consecutive gaps must equal the period.
        for (int i = 1; i < grid.Length; i++)
        {
            Assert.InRange(grid[i] - grid[i - 1], period - Epsilon, period + Epsilon);
        }
    }

    [Fact]
    public void Synthesize_AnchorIsRobustToOutlierDownbeats()
    {
        // 60 "correct" downbeats every 2s starting at 0.7, plus 5 outliers from a
        // misdetected intro (closer-spaced, wrong phase). The synthesized anchor
        // should track the majority cluster, not the outliers.
        double bpm = 120.0;
        double period = 2.0;
        var correct = Enumerable.Range(0, 60).Select(i => 0.7 + i * period).ToArray();
        var outliers = new[] { 0.05, 0.35, 0.95, 1.25, 1.55 }; // bunched at song start
        var raw = correct.Concat(outliers).OrderBy(x => x).ToArray();
        var beats = Enumerable.Range(0, 240).Select(i => 0.7 + i * 0.5).ToArray();

        var grid = Beatgrid.Synthesize(bpm, beats, raw, durationSec: 120.0);

        // The grid's anchor (first non-negative entry) should match 0.7 within tolerance,
        // not be dragged toward the outlier cluster around 0.0–1.5.
        var anchor = grid[0] % period;
        if (anchor < 0) anchor += period;
        Assert.InRange(anchor, 0.7 - 0.1, 0.7 + 0.1);
    }

    [Fact]
    public void Synthesize_DetectsThreeFourTime()
    {
        // 90 BPM in 3/4 → bar period = 2.0 s. Beats every 0.667 s.
        double bpm = 90.0;
        double beatGap = 60.0 / bpm;        // ≈ 0.667
        double period = beatGap * 3;        // 2.0
        var beats = Enumerable.Range(0, 90).Select(i => i * beatGap).ToArray();
        var downbeats = Enumerable.Range(0, 30).Select(i => i * period).ToArray();

        var grid = Beatgrid.Synthesize(bpm, beats, downbeats, durationSec: 60.0);

        Assert.True(grid.Length >= 25);
        for (int i = 1; i < grid.Length; i++)
        {
            Assert.InRange(grid[i] - grid[i - 1], period - Epsilon, period + Epsilon);
        }
    }

    [Fact]
    public void Synthesize_GridStartsAtOrBeforeFirstRealDownbeat()
    {
        // Anchor is mid-song. We expect the grid to walk *back* from the anchor
        // to cover the start of the song too, not skip it.
        double bpm = 120.0;
        double period = 2.0;
        var downbeats = Enumerable.Range(20, 40).Select(i => 0.5 + i * period).ToArray();
        var beats = Enumerable.Range(0, 240).Select(i => i * 0.5).ToArray();

        var grid = Beatgrid.Synthesize(bpm, beats, downbeats, durationSec: 120.0);

        Assert.True(grid[0] < 2.5, $"first grid line should be near song start, was {grid[0]}");
    }

    [Fact]
    public void Synthesize_ReturnsEmptyOnZeroBpm()
    {
        var grid = Beatgrid.Synthesize(bpm: 0, rawBeats: [], rawDownbeats: [], durationSec: 60.0);
        Assert.Empty(grid);
    }

    [Fact]
    public void Synthesize_HandlesNoDownbeats()
    {
        // BPM known but no downbeats detected — should return a grid anchored at 0.
        double bpm = 120.0;
        var grid = Beatgrid.Synthesize(bpm, rawBeats: [0.5, 1.0, 1.5, 2.0], rawDownbeats: [], durationSec: 60.0);
        Assert.NotEmpty(grid);
        // Every gap is the bar period.
        double period = 60.0 / bpm * 4;
        for (int i = 1; i < grid.Length; i++)
        {
            Assert.InRange(grid[i] - grid[i - 1], period - Epsilon, period + Epsilon);
        }
    }
}
