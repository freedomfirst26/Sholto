namespace Sholto.Bench.Sound;

/// <summary>ffmpeg <c>astats</c> per-channel summary.</summary>
public sealed class ChannelStats
{
    public int Channel { get; init; }
    public double? RmsLevelDb { get; init; }
    public double? PeakLevelDb { get; init; }
}
