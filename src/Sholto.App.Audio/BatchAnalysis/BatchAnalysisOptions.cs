namespace Sholto.App.Audio.BatchAnalysis;

/// <summary>Whole-library (batch) analysis tuning, supplied via <c>IOptions&lt;BatchAnalysisOptions&gt;</c>.</summary>
public sealed class BatchAnalysisOptions
{
    /// <summary>How many tracks may be decoding or analysing at once. Each one in flight holds its
    /// decoded buffer (~92 MB for a 4-minute track) and a beat-tracker process, so this bounds both
    /// memory and CPU. Values below 1 are treated as 1.</summary>
    public int MaxParallelTracks { get; set; } = 2;
}
