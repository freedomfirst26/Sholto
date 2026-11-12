namespace Sholto.App.Lifecycle.Folders;

/// <summary>Moves the stems from one analysis folder to another. File work only: the caller holds the stem
/// gate, validates the folders and owns what "in use" means.</summary>
public interface IStemFolderMover
{
    /// <summary>Move <c>&lt;oldRoot&gt;/stems</c> to <c>&lt;newRoot&gt;/stems</c>. One rename when both are on
    /// the same filesystem and the target is absent; otherwise per track folder, by rename or, across
    /// drives, copy then verify. <paramref name="commit"/> runs once every folder is placed and before any
    /// copied source is deleted; if it throws the move is undone (renames reversed) and fails. A failure
    /// leaves the old folder as it was: the source is never deleted before the commit.</summary>
    /// <param name="progress">Called with (done, total) track folders, first with 0 done.</param>
    Task<StemMoveOutcome> MoveAsync(string oldRoot, string newRoot, Action<int, int> progress, Func<Task> commit);
}
