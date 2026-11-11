using Sholto.App.Library.Markers;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>A hot-cue store whose writes always fail, and whose list fails when asked to.</summary>
internal sealed class ThrowingHotCueStore(bool listFails) : IHotCueStore
{
    private readonly bool _listFails = listFails;

    public List<string> Calls { get; } = [];

    public Task<IReadOnlyList<HotCue>> ListAsync(Guid trackId) =>
        _listFails
            ? Task.FromException<IReadOnlyList<HotCue>>(new InvalidOperationException("db down"))
            : Task.FromResult<IReadOnlyList<HotCue>>([]);

    public Task SetAsync(Guid trackId, HotCue cue)
    {
        Calls.Add($"set {cue.Pad}");
        return Task.FromException(new InvalidOperationException("db down"));
    }

    public Task DeleteAsync(Guid trackId, int pad)
    {
        Calls.Add($"delete {pad}");
        return Task.FromException(new InvalidOperationException("db down"));
    }
}
