using Sholto.App.Library.Markers;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Hot cues in memory whose list and write calls can be held pending and released by the test, so ordering
/// is deterministic. Completion sources run continuations inline, so a synchronous test sees the whole chain.</summary>
internal sealed class GatedHotCueStore : IHotCueStore
{
    private readonly Dictionary<Guid, Dictionary<int, HotCue>> _cues = [];
    private readonly List<(Guid TrackId, TaskCompletionSource<IReadOnlyList<HotCue>> Done)> _pendingLists = [];
    private readonly List<(Action Commit, TaskCompletionSource Done)> _pendingWrites = [];

    public List<string> Calls { get; } = [];
    public bool HoldLists { get; set; }
    public bool HoldWrites { get; set; }

    public void Seed(Guid trackId, HotCue cue) => Put(trackId, cue);

    public HotCue? Committed(Guid trackId, int pad) =>
        _cues.TryGetValue(trackId, out var pads) && pads.TryGetValue(pad, out var cue) ? cue : null;

    public Task<IReadOnlyList<HotCue>> ListAsync(Guid trackId)
    {
        if (!HoldLists) return Task.FromResult(Snapshot(trackId));
        var done = new TaskCompletionSource<IReadOnlyList<HotCue>>();
        _pendingLists.Add((trackId, done));
        return done.Task;
    }

    public void ReleaseLists()
    {
        HoldLists = false;
        var pending = _pendingLists.ToArray();
        _pendingLists.Clear();
        foreach (var (trackId, done) in pending) done.SetResult(Snapshot(trackId));
    }

    public Task SetAsync(Guid trackId, HotCue cue)
    {
        Calls.Add($"set {cue.Pad}");
        return Write(() => Put(trackId, cue));
    }

    public Task DeleteAsync(Guid trackId, int pad)
    {
        Calls.Add($"delete {pad}");
        if (Committed(trackId, pad) is null) return Task.CompletedTask;   // no committed row yet: nothing to delete
        return Write(() => _cues[trackId].Remove(pad));
    }

    /// <summary>Stops holding, then commits the pending writes in call order. Each is committed before its task
    /// completes, so a continuation that runs inline already sees it.</summary>
    public void ReleaseWrites()
    {
        HoldWrites = false;
        var pending = _pendingWrites.ToArray();
        _pendingWrites.Clear();
        foreach (var (commit, done) in pending)
        {
            commit();
            done.SetResult();
        }
    }

    private Task Write(Action commit)
    {
        if (!HoldWrites)
        {
            commit();
            return Task.CompletedTask;
        }
        var done = new TaskCompletionSource();
        _pendingWrites.Add((commit, done));
        return done.Task;
    }

    private void Put(Guid trackId, HotCue cue)
    {
        if (!_cues.TryGetValue(trackId, out var pads)) _cues[trackId] = pads = [];
        pads[cue.Pad] = cue;
    }

    private IReadOnlyList<HotCue> Snapshot(Guid trackId) =>
        _cues.TryGetValue(trackId, out var pads) ? pads.Values.OrderBy(c => c.Pad).ToList() : [];
}
