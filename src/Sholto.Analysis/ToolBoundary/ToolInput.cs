namespace Sholto.Analysis.ToolBoundary;

/// <summary>
/// The file an external tool run analyses, and the directory the run writes into.
/// These two always travel together through the external-tool stack — the directory
/// demucs writes into and the directory the presence probe later looks in must derive
/// from the exact same value, so they are carried as one unit rather than as two
/// independently-passable strings.
/// </summary>
public readonly record struct ToolInput(string Input, string WorkDir);
