using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>What Glance knows of the Track List (count, keys, paths), the Ctrl+Del clear-arm, and the
/// commands that add to or clear the list.</summary>
public interface IGlanceTrackList
{
    /// <summary>The clear-arm came on or went off.</summary>
    event Action? ClearArmedChanged;

    int Count { get; }

    /// <summary><c>crate:&lt;id&gt;</c> and <c>tag:&lt;lower name&gt;</c> of the list's sources.</summary>
    IReadOnlySet<string> Keys { get; }

    /// <summary>"Ctrl Del again: clear N" while armed, else null.</summary>
    string? ClearArmedText { get; }

    bool Contains(string path);

    /// <summary>Store the list's new count, keys and paths. Raises nothing.</summary>
    void Apply(TrackListChanged e);

    /// <summary>Drop the clear-arm when the list is empty.</summary>
    void DisarmIfEmpty();

    /// <summary>What Ctrl+L would add: the highlighted song, or the highlighted crate or tag in the rail.</summary>
    GlanceLoadState LoadState(string? highlightedPath, object? railItem);

    int LoadCount(string? highlightedPath, object? railItem);

    /// <summary>Send what Ctrl+L would add, unless it is already in the list.</summary>
    void Load(string? highlightedPath, object? railItem);

    /// <summary>Star: remove the song from the list if it is in it, else add it.</summary>
    void Toggle(string path);

    /// <summary>Ctrl+Del: arm, and on the second press within the window clear the list.</summary>
    void Clear();

    void Disarm();

    /// <summary>Drop the clear-arm once its window has passed.</summary>
    void Tick(DateTime now);
}
