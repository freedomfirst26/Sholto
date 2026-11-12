using Sholto.App.Lifecycle.Folders;

namespace Sholto.App.Tests;

/// <summary>A mover that records what it was asked and runs a scripted behaviour: by default it reports one
/// step of progress, runs the commit and succeeds.</summary>
internal sealed class FakeStemFolderMover : IStemFolderMover
{
    public List<(string Old, string New)> Calls { get; } = [];

    public Func<Action<int, int>, Func<Task>, Task<StemMoveOutcome>>? Behaviour { get; set; }

    public async Task<StemMoveOutcome> MoveAsync(string oldRoot, string newRoot, Action<int, int> progress, Func<Task> commit)
    {
        Calls.Add((oldRoot, newRoot));
        if (Behaviour is not null) return await Behaviour(progress, commit);
        progress(1, 1);
        await commit();
        return new StemMoveOutcome(true, null);
    }
}
