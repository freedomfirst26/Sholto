namespace Sholto.App.Audio.BatchAnalysis;

/// <summary>One progress report from a batch analysis: <paramref name="Error"/> is set only for
/// <see cref="BatchTrackState.Failed"/>.</summary>
public readonly record struct BatchTrackProgress(string Path, BatchTrackState State, string? Error = null);
