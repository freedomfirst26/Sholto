using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>The catalog (every scanned song), the visible rows (the catalog until a Track List is shown,
/// then that list) and the highlighted row. Every call happens on the app thread.</summary>
public interface ILibraryCatalog
{
    /// <summary>The live catalog list, in scan order.</summary>
    IReadOnlyList<TrackSummary> All { get; }

    /// <summary>The live visible rows.</summary>
    IReadOnlyList<TrackSummary> Rows { get; }

    /// <summary>The highlighted visible row, or -1.</summary>
    int SelectedIndex { get; }

    TrackSummary? SelectedSummary { get; }

    /// <summary>The visible rows changed; carries one immutable snapshot.</summary>
    event Action<IReadOnlyList<TrackSummary>>? RowsChanged;

    /// <summary>One row's summary was replaced.</summary>
    event Action<TrackSummary>? RowUpdated;

    event Action? SelectedIndexChanged;

    /// <summary>Make <paramref name="summaries"/> the catalog (the list itself is stored, not copied) and refresh the visible rows.</summary>
    void ReplaceAll(List<TrackSummary> summaries);

    void ShowTrackList(IReadOnlyList<string> paths);

    /// <summary>Store a changed summary in the catalog and the visible rows and announce it.</summary>
    void Replace(int allIndex, TrackSummary updated);

    bool TryFind(string filePath, out int allIndex, out TrackSummary summary);

    void SetSelectedIndex(int index);

    void Select(int index);

    void Rotate(int delta);
}
