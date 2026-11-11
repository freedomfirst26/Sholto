namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The crates and tags the Glance rail offers for the free words of the query, and where the
/// highlight lands when the rail is rebuilt.</summary>
public interface IGlanceRail
{
    /// <summary>Crates and tags were replaced; the rail is to be rebuilt.</summary>
    event Action? Changed;

    /// <summary>Look the crates and tags up for <paramref name="railText"/>. With the database down they are emptied
    /// and <see cref="Changed"/> is raised before this returns; otherwise it is raised when the newest answer lands.</summary>
    void Refresh(string railText, bool databaseUp);

    /// <summary>The rail items over the crates and tags held now.</summary>
    IReadOnlyList<object> Items(IReadOnlyList<GlanceChip> chips, IReadOnlyDictionary<int, int>? crateCounts,
        IReadOnlyDictionary<string, int>? tagCounts, IReadOnlySet<string> trackListKeys);

    /// <summary>Where the highlight goes in <paramref name="newItems"/>: on the item that was highlighted in
    /// <paramref name="oldItems"/> if it is still there, else on the first item that is not a header.</summary>
    int IndexAfterRebuild(IReadOnlyList<object> oldItems, int oldIndex, IReadOnlyList<object> newItems);

    /// <summary>The first item that is not a header, or 0.</summary>
    int FirstSelectable(IReadOnlyList<object> items);
}
