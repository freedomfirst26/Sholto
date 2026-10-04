using Sholto.App.Audio;

namespace Sholto.App.Tests;

/// <summary>A beatgrid that records the calls it gets and does nothing else.</summary>
internal sealed class F9SpyBeatgrid : IDeckBeatgrid
{
    public List<double> BpmAdjustments { get; } = [];
    public List<double> FineNudges { get; } = [];
    public List<(double A, double B)> TwoPointGrids { get; } = [];
    public int Resets { get; private set; }

    public bool IsGridNudged => false;

    public event Action<bool>? GridNudgedChanged
    {
        add { }
        remove { }
    }

    public void ApplyGridAdjustment(double? bpmOverride, double offsetSec)
    {
    }

    public void AdjustBpm(double deltaBpm) => BpmAdjustments.Add(deltaBpm);

    public void NudgeGrid(int beats)
    {
    }

    public void NudgeGridFine(double seconds) => FineNudges.Add(seconds);

    public void SetGridFromTwoPoints(double tA, double tB) => TwoPointGrids.Add((tA, tB));

    public void ResetGrid() => Resets++;
}
