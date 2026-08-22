using Sholto.Analysis;
using Xunit;

namespace Sholto.App.Tests;

public class BeatgridAnchorTests
{
    // Reproduces the real failure: madmom's beat positions random-walk over a
    // track, so downbeats late in the track sit at a very different phase than the
    // opening bars. Averaging across all of them (the old ComputeAnchor) dragged
    // the anchor ~a beat off even at the start. The grid must anchor to the
    // OPENING downbeats so the start lines up.
    [Fact]
    public void Anchor_LocksToOpeningBars_WhenLaterBeatsWander()
    {
        const double bpm = 175.0;
        double beatPeriod = 60.0 / bpm;
        double barPeriod = beatPeriod * 4;
        const double openingPhase = 0.11;

        var beats = new List<double>();
        var downbeats = new List<double>();
        for (int bar = 0; bar < 60; bar++)
        {
            // Opening bars steady at openingPhase; later bars drift up to +0.6s
            // (a big madmom-style wander) so their phase is far from the opening.
            double drift = bar < 8 ? 0.0 : System.Math.Min(0.6, (bar - 8) * 0.03);
            double db = openingPhase + bar * barPeriod + drift;
            downbeats.Add(db);
            for (int j = 0; j < 4; j++) beats.Add(db + j * beatPeriod);
        }
        double dur = beats[^1] + 2.0;

        var (_, gridDownbeats) = Beatgrid.SynthesizeFullGrid(bpm, beats.ToArray(), downbeats.ToArray(), dur);

        Assert.NotEmpty(gridDownbeats);
        // First grid downbeat lines up with the opening downbeats, not the
        // smeared whole-track average.
        Assert.InRange(gridDownbeats[0], openingPhase - 0.05, openingPhase + 0.05);
    }
}
