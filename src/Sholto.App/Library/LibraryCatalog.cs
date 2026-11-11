using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>See <see cref="ILibraryCatalog"/>. The catalog (<c>_all</c>) is the source of truth; the visible rows
/// (<c>_visible</c>) are the catalog until a Track List is shown, then the Track List's paths (those in the
/// catalog), in its order.</summary>
public sealed class LibraryCatalog(IEventPublisher publisher) : ILibraryCatalog
{
    private readonly IEventPublisher _publisher = publisher;

    private List<TrackSummary> _all = [];
    private List<TrackSummary> _visible = [];
    private readonly Dictionary<string, int> _allIndex = [];
    private readonly Dictionary<string, int> _visibleIndex = [];
    private int _version;
    private int _selectedIndex = -1;
    private List<string>? _shownPaths;

    public event Action<IReadOnlyList<TrackSummary>>? RowsChanged;
    public event Action<TrackSummary>? RowUpdated;
    public event Action? SelectedIndexChanged;

    public IReadOnlyList<TrackSummary> All => _all;

    public IReadOnlyList<TrackSummary> Rows => _visible;

    public int SelectedIndex => _selectedIndex;

    public TrackSummary? SelectedSummary =>
        _selectedIndex >= 0 && _selectedIndex < _visible.Count ? _visible[_selectedIndex] : null;

    public void ReplaceAll(List<TrackSummary> summaries)
    {
        _all = summaries;
        _allIndex.Clear();
        for (var i = 0; i < _all.Count; i++) _allIndex[_all[i].FilePath] = i;
        SetVisible(VisibleRows());
    }

    /// <summary>The rows to show now: the whole catalog until a Track List is first shown, then the shown
    /// paths that the catalog holds, in the shown order.</summary>
    private List<TrackSummary> VisibleRows()
    {
        if (_shownPaths is null) return [.. _all];
        var rows = new List<TrackSummary>(_shownPaths.Count);
        foreach (var path in _shownPaths)
            if (_allIndex.TryGetValue(path, out var i)) rows.Add(_all[i]);
        return rows;
    }

    private void SetVisible(List<TrackSummary> rows)
    {
        // Keep the highlight on the same song; when it is gone, on the same row (clamped).
        var highlighted = SelectedSummary?.FilePath;
        var oldIndex = _selectedIndex;
        _visible = rows;
        _visibleIndex.Clear();
        for (var i = 0; i < _visible.Count; i++) _visibleIndex[_visible[i].FilePath] = i;
        _version++;
        var newIndex = NewSelectedIndex(highlighted, oldIndex);
        if (newIndex == _selectedIndex && newIndex >= 0 && SelectedSummary?.FilePath != highlighted)
        {
            // Same row number, different song: the index did not change but the selection did.
            SelectedIndexChanged?.Invoke();
            _publisher.Publish(new SelectionChanged(_selectedIndex, SelectedSummary?.TrackId ?? Guid.Empty));
        }
        else SetSelectedIndex(newIndex);
        // One immutable snapshot, shared by the C# event and the bus.
        var snapshot = _visible.ToArray();
        RowsChanged?.Invoke(snapshot);
        _publisher.Publish(new LibraryRowsChanged(snapshot, _version));
    }

    public void Replace(int allIndex, TrackSummary updated)
    {
        _all[allIndex] = updated;
        if (_visibleIndex.TryGetValue(updated.FilePath, out var visibleIndex)) _visible[visibleIndex] = updated;
        RowUpdated?.Invoke(updated);
        _publisher.Publish(new TrackSummaryChanged(updated));
    }

    public bool TryFind(string filePath, out int allIndex, out TrackSummary summary)
    {
        if (_allIndex.TryGetValue(filePath, out allIndex))
        {
            summary = _all[allIndex];
            return true;
        }
        summary = null!;
        return false;
    }

    public void ShowTrackList(IReadOnlyList<string> paths)
    {
        _shownPaths = [.. paths];
        SetVisible(VisibleRows());
    }

    private int NewSelectedIndex(string? highlighted, int oldIndex)
    {
        if (oldIndex < 0 || _visible.Count == 0) return -1;
        if (highlighted is not null && _visibleIndex.TryGetValue(highlighted, out var same)) return same;
        return Math.Clamp(oldIndex, 0, _visible.Count - 1);
    }

    public void SetSelectedIndex(int index)
    {
        if (_selectedIndex == index) return;
        _selectedIndex = index;
        SelectedIndexChanged?.Invoke();
        _publisher.Publish(new SelectionChanged(_selectedIndex, SelectedSummary?.TrackId ?? Guid.Empty));
    }

    public void Select(int index)
    {
        if (_visible.Count == 0) return;
        SetSelectedIndex(Math.Clamp(index, 0, _visible.Count - 1));
    }

    public void Rotate(int delta)
    {
        if (_visible.Count == 0) return;
        int next = _selectedIndex < 0 ? 0 : _selectedIndex + delta;
        Select(next);
    }
}
