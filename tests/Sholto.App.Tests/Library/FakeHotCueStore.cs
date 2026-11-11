using Sholto.App.Library.Markers;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Hot cues held in memory, per track.</summary>
internal sealed class FakeHotCueStore : IHotCueStore
{
    private readonly Dictionary<Guid, Dictionary<int, HotCue>> _cues = [];

    public List<string> Calls { get; } = [];

    public Task<IReadOnlyList<HotCue>> ListAsync(Guid trackId) =>
        Task.FromResult<IReadOnlyList<HotCue>>(
            _cues.TryGetValue(trackId, out var pads) ? pads.Values.OrderBy(c => c.Pad).ToList() : []);

    public Task SetAsync(Guid trackId, HotCue cue)
    {
        Calls.Add($"set {cue.Pad}");
        if (!_cues.TryGetValue(trackId, out var pads)) _cues[trackId] = pads = [];
        pads[cue.Pad] = cue;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid trackId, int pad)
    {
        Calls.Add($"delete {pad}");
        if (_cues.TryGetValue(trackId, out var pads)) pads.Remove(pad);
        return Task.CompletedTask;
    }
}
