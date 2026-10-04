namespace Sholto.Data;

/// <summary>One library track as the App currently knows it: the scanned file plus every slow-to-arrive
/// fact about it (BPM, key, stems, tags, played this run, analysis progress). Immutable: a change is a new
/// summary, announced by <see cref="TrackSummaryChanged"/> or a new <see cref="LibraryRowsChanged"/>.
/// Interfaces project it and never modify it.</summary>
public sealed record TrackSummary(string FilePath, string Title, string Artist, TimeSpan Duration)
{
    /// <summary>Catalog id; <see cref="Guid.Empty"/> when the track is not in the catalog (no database).</summary>
    public Guid TrackId { get; init; }

    /// <summary>Raw BPM as detected (null before analysis), before the user's multiplier.</summary>
    public double? Bpm { get; init; }

    /// <summary>The user's half/double override, persisted per file.</summary>
    public double BpmMultiplier { get; init; } = 1.0;

    /// <summary>The four stem files already exist for this track.</summary>
    public bool StemsReady { get; init; }

    public KeyRef? MusicalKey { get; init; }

    /// <summary>Loaded into a deck during this run.</summary>
    public bool IsPlayed { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>An analysis step is running for this track.</summary>
    public bool IsAnalyzing { get; init; }

    /// <summary>The reporter's failure text, or null when nothing failed.</summary>
    public string? AnalysisFailure { get; init; }

    /// <summary>The failure includes a required step (beat detection).</summary>
    public bool HasRequiredFailure { get; init; }
}
