using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>The DJ's shortlist: tracks set aside before a set, in insertion order. Toggled by
/// <see cref="ToggleShortlist"/>, announced as <see cref="ShortlistChanged"/> (catalog tracks only), and saved by the
/// lifecycle. A listed path that is not in the catalog (an unmounted drive) is kept, just not shown. App thread only.</summary>
public interface IShortlist : ICommandHandler<ToggleShortlist>
{
    /// <summary>Every listed path in insertion order, including paths the catalog does not hold now.</summary>
    IReadOnlyList<string> Paths { get; }

    /// <summary>The user toggled a track, so the list should be saved. Not raised by <see cref="Restore"/>.</summary>
    event Action? Changed;

    /// <summary>Put back a saved list: the saved paths first, then any path toggled in before the restore landed.
    /// Announces, but does not raise <see cref="Changed"/>.</summary>
    void Restore(IReadOnlyList<string> paths);
}
