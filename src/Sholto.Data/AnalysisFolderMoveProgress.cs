namespace Sholto.Data;

/// <summary>Progress of moving the stems to a new analysis folder, one step per track folder. A fact: not replayed.</summary>
/// <param name="Done">Track folders placed so far.</param>
/// <param name="Total">Track folders to place in all.</param>
public readonly record struct AnalysisFolderMoveProgress(int Done, int Total) : IEvent;
