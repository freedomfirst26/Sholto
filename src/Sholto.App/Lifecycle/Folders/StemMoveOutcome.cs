namespace Sholto.App.Lifecycle.Folders;

/// <summary>How <see cref="IStemFolderMover.MoveAsync"/> ended.</summary>
/// <param name="Succeeded">True when every stem folder is in the new place and the commit ran.</param>
/// <param name="Problem">Why it failed, worded for the user; null on success.</param>
public readonly record struct StemMoveOutcome(bool Succeeded, string? Problem);
