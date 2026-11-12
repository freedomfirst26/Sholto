namespace Sholto.Data;

/// <summary>One row of <see cref="FoldersNeeded"/>: a folder the user can see and change.</summary>
/// <param name="Path">The folder, or the suggestion for a <see cref="FolderRowState.NotChosen"/> row. Null when there is none.</param>
/// <param name="State">Where the row stands.</param>
/// <param name="Problem">Why the path is not acceptable, worded for the user; null when it is fine.</param>
public readonly record struct FolderRow(string? Path, FolderRowState State, string? Problem);
