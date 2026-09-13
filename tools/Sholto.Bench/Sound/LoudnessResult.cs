namespace Sholto.Bench.Sound;

/// <summary>ffmpeg <c>ebur128</c> integrated-loudness summary.</summary>
public sealed class LoudnessResult
{
    public double? IntegratedLufs { get; init; }
    public double? LoudnessRangeLu { get; init; }
    public double? TruePeakDbfs { get; init; }
}
