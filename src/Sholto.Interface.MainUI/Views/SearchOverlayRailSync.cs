using Avalonia.Controls;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <inheritdoc cref="ISearchOverlayRailSync"/>
internal sealed class SearchOverlayRailSync : ISearchOverlayRailSync
{
    private readonly ListBox _rail;
    private readonly Dictionary<string, int> _railCounts = [];
    private readonly HashSet<string> _tickKeys = [];
    private bool _syncing;

    public SearchOverlayRailSync(ListBox rail)
    {
        _rail = rail;
        _rail.ContainerPrepared += OnContainerPrepared;
    }

    public void Sync(IGlanceViewModel glance)
    {
        _syncing = true;
        try
        {
            MarkChangedCounts(glance);
            _rail.ItemsSource = glance.RailItems;
            _rail.SelectedIndex = glance.RailIndex;
        }
        finally { _syncing = false; }
        ScrollToHighlight(glance);
    }

    public void SyncHighlight(IGlanceViewModel glance)
    {
        _syncing = true;
        try { _rail.SelectedIndex = glance.RailIndex; }
        finally { _syncing = false; }
        ScrollToHighlight(glance);
    }

    public bool Pick(IGlanceViewModel glance)
    {
        if (_syncing) return false;
        var picked = _rail.SelectedIndex;
        if (picked >= 0 && glance.RailItems[picked] is not GlanceRailHeader)
        {
            if (glance.Zone != GlanceZone.Rail) glance.ToggleZone();
            glance.Move(SelectableSteps(glance.RailItems, glance.RailIndex, picked));
        }
        else
        {
            // A header cannot be highlighted; put the list's selection back where the view model has it.
            SyncHighlight(glance);
        }
        return true;
    }

    private void ScrollToHighlight(IGlanceViewModel glance)
    {
        if (glance.RailIndex >= 0 && glance.RailIndex < glance.RailItems.Count)
            _rail.ScrollIntoView(glance.RailIndex);
    }

    /// <summary>Which crates and tags show a different count than last time: their rows tick as they are built.</summary>
    private void MarkChangedCounts(IGlanceViewModel glance)
    {
        _tickKeys.Clear();
        foreach (var item in glance.RailItems)
        {
            var (key, count) = RailCount(item);
            if (key is null) continue;
            if (_railCounts.TryGetValue(key, out var before) && before != count && glance.AnimateResults) _tickKeys.Add(key);
            _railCounts[key] = count;
        }
    }

    private (string? Key, int Count) RailCount(object item) => item switch
    {
        GlanceRailCrate c => ("c" + c.Id, c.Count),
        GlanceRailTag t => ("t" + t.Name, t.Count),
        _ => (null, 0),
    };

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem box) return;
        var (key, _) = RailCount(box.DataContext ?? _rail.Items[e.Index]!);
        // Containers are recycled, so the class is set either way. Each key ticks once.
        box.Classes.Set("tick", key is not null && _tickKeys.Remove(key));
    }

    /// <summary>How many <see cref="IGlanceViewModel.Move"/> steps (headers skipped) lead from
    /// <paramref name="from"/> to <paramref name="to"/>, signed.</summary>
    private int SelectableSteps(IReadOnlyList<object> items, int from, int to)
    {
        if (to == from) return 0;
        var step = to > from ? 1 : -1;
        var count = 0;
        for (var i = from + step; i >= 0 && i < items.Count; i += step)
        {
            if (items[i] is not GlanceRailHeader) count++;
            if (i == to) break;
        }
        return count * step;
    }
}
