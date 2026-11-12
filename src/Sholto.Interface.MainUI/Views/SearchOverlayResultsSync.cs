using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Sholto.Interface.MainUI.Controls;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <inheritdoc cref="ISearchOverlayResultsSync"/>
internal sealed class SearchOverlayResultsSync : ISearchOverlayResultsSync
{
    /// <summary>How long a row that joined the Track List glows.</summary>
    private const int FlashMs = 600;

    private readonly ListBox _results;
    private readonly FitShimmerColumn _shimmer;
    private readonly TextBlock _emptyHint;
    private readonly TextBlock _scopeEmptyHint;
    private bool _syncing;

    public SearchOverlayResultsSync(ListBox results, FitShimmerColumn shimmer, TextBlock emptyHint, TextBlock scopeEmptyHint)
    {
        _results = results;
        _shimmer = shimmer;
        _emptyHint = emptyHint;
        _scopeEmptyHint = scopeEmptyHint;
        _results.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrolled);
    }

    public void Sync(IGlanceViewModel glance)
    {
        _syncing = true;
        try
        {
            _results.ItemsSource = glance.Rows;
            _shimmer.RowCount = glance.Rows.Count;
            SyncEmpty(glance);
            _results.SelectedIndex = glance.Rows.Count == 0 ? -1 : glance.TableIndex;
        }
        finally { _syncing = false; }
        ScrollToHighlight(glance);
    }

    public void SyncEmpty(IGlanceViewModel glance)
    {
        var none = glance.Rows.Count == 0;
        var scoped = glance.ScopeEmptyText;
        _emptyHint.IsVisible = none && scoped is null;
        _scopeEmptyHint.IsVisible = none && scoped is not null;
        if (!_scopeEmptyHint.IsVisible) return;

        // The names in bright text: every chip's name and the quoted free text.
        var names = new List<string>();
        foreach (var chip in glance.Chips) names.Add(chip.Name);
        var open = scoped!.IndexOf('“');
        var close = open < 0 ? -1 : scoped.IndexOf('”', open + 1);
        if (open >= 0 && close > open) names.Add(scoped.Substring(open + 1, close - open - 1));

        _scopeEmptyHint.Inlines ??= new InlineCollection();
        _scopeEmptyHint.Inlines.Clear();
        var at = 0;
        while (at < scoped.Length)
        {
            var next = -1;
            var length = 0;
            foreach (var name in names)
            {
                if (name.Length == 0) continue;
                var found = scoped.IndexOf(name, at, StringComparison.Ordinal);
                if (found >= 0 && (next < 0 || found < next)) { next = found; length = name.Length; }
            }
            if (next < 0) { _scopeEmptyHint.Inlines.Add(new Run(scoped[at..])); break; }
            if (next > at) _scopeEmptyHint.Inlines.Add(new Run(scoped[at..next]));
            var bright = new Run(scoped.Substring(next, length)) { FontWeight = FontWeight.SemiBold };
            bright.Bind(TextElement.ForegroundProperty, _scopeEmptyHint.GetResourceObservable("SholtoTextBright"));
            _scopeEmptyHint.Inlines.Add(bright);
            at = next + length;
        }
    }

    public void SyncHighlight(IGlanceViewModel glance)
    {
        _syncing = true;
        try { _results.SelectedIndex = glance.Rows.Count == 0 ? -1 : glance.TableIndex; }
        finally { _syncing = false; }
        ScrollToHighlight(glance);
    }

    public void SyncReassessing(IGlanceViewModel glance)
    {
        _results.Classes.Set("reassessing", glance.IsReassessing);
        _shimmer.IsVisible = glance.IsReassessing;
        _shimmer.Classes.Set("running", glance.IsReassessing && glance.AnimateResults);
    }

    public void Flash(IGlanceViewModel glance, string path)
    {
        if (!glance.AnimateResults) return;
        for (var i = 0; i < glance.Rows.Count; i++)
        {
            if (glance.Rows[i].FilePath != path) continue;
            if (_results.ContainerFromIndex(i) is not ListBoxItem item) return;
            // Off then on, so a second add mid-flash restarts it.
            item.Classes.Set("flash", false);
            item.Classes.Set("flash", true);
            DispatcherTimer.RunOnce(() => item.Classes.Set("flash", false), TimeSpan.FromMilliseconds(FlashMs));
            return;
        }
    }

    public bool Pick(IGlanceViewModel glance)
    {
        if (_syncing) return false;
        var picked = _results.SelectedIndex;
        if (picked >= 0)
        {
            if (glance.Zone != GlanceZone.Table) glance.ToggleZone();
            glance.Move(picked - glance.TableIndex);
        }
        return true;
    }

    private void OnScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll) _shimmer.ScrollOffset = scroll.Offset.Y;
    }

    private void ScrollToHighlight(IGlanceViewModel glance)
    {
        if (glance.TableIndex >= 0 && glance.TableIndex < glance.Rows.Count)
            _results.ScrollIntoView(glance.TableIndex);
    }
}
