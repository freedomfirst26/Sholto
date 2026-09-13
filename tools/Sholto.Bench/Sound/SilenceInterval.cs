namespace Sholto.Bench.Sound;

/// <summary>One ffmpeg <c>silencedetect</c> hit.</summary>
public sealed class SilenceInterval
{
    public double StartSeconds { get; init; }
    public double? EndSeconds { get; init; }
    public double? DurationSeconds { get; init; }
}
