namespace Sholto.Data;

/// <summary>The answer to <see cref="CheckFolders"/>. A null problem means that folder is acceptable.</summary>
/// <param name="MusicProblem">Why the music folder is not acceptable, worded for the user; null when it is fine.</param>
/// <param name="AnalysisProblem">Why the analysis folder is not acceptable, worded for the user; null when it is fine.</param>
/// <param name="SuggestedAnalysis">The analysis folder to offer while the user has not picked one; null when there is no music folder to base it on.</param>
public readonly record struct FolderCheckResult(string? MusicProblem, string? AnalysisProblem, string? SuggestedAnalysis);
