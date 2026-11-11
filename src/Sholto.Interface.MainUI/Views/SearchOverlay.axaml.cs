using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Views;

/// <summary>
/// The Glance search overlay. The XAML carries the look and the bindings; this file carries what only a view
/// can: key routing into the Glance view model, keeping the two lists' highlight on the view model's,
/// the motion (the results settling in,
/// the slow indicator, a rail count ticking) that the view model only cues.
/// </summary>
public partial class SearchOverlay : UserControl
{
    /// <summary>How long the "fresh" class stays on the table after a result: past the 160 ms fade.</summary>
    private const int SettleHoldMs = 260;

    /// <summary>How long a row that joined the Track List glows.</summary>
    private const int FlashMs = 600;

    private const string DefaultWatermark = "Name, initials (bsn), bpm:128, key:8A, #tag";
    private const string ChipWatermark = "Filter further…";

    private MainViewModel? _vm;
    private bool _syncing;
    private readonly DispatcherTimer _settleTimer;
    private readonly ISearchOverlayKeyHandler _keys;
    private readonly ISearchOverlayRailSync _rail;

    public SearchOverlay()
    {
        InitializeComponent();
        // Forced: XAML constructs this control, and the named controls only exist after InitializeComponent (as WaveformControl).
        _keys = new SearchOverlayKeyHandler(QueryBox);
        _rail = new SearchOverlayRailSync(RailList);
        // Tunnel, so the box's own handling of arrows, Tab and Enter never sees these keys.
        QueryBox.AddHandler(KeyDownEvent, (_, e) => _keys.OnKeyDown(Glance, e), RoutingStrategies.Tunnel);
        QueryBox.AddHandler(TextInputEvent, (_, e) => _keys.OnTextInput(e), RoutingStrategies.Tunnel);
        QueryBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.SelectionStartProperty
                || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.TextProperty)
                SyncCompletion();
        };
        // Tunnel: the list selects on press and marks it handled, so a click on a crate or tag is read before that.
        RailList.AddHandler(PointerPressedEvent, OnRailPressed, RoutingStrategies.Tunnel);
        ResultsList.AddHandler(ScrollViewer.ScrollChangedEvent, OnResultsScrolled);
        _settleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SettleHoldMs) };
        _settleTimer.Tick += (_, _) =>
        {
            _settleTimer.Stop();
            ResultsList.Classes.Set("fresh", false);
        };
        DataContextChanged += (_, _) => Attach();
        // Focus the input every time the overlay becomes visible so the person can type at once.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
                Dispatcher.UIThread.Post(() => QueryBox.Focus());
        };
    }

    private IGlanceViewModel? Glance => _vm?.Glance;

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.Glance.PropertyChanged -= OnGlanceChanged;
            _vm.Glance.SelectAllOnOpen -= OnSelectAllOnOpen;
            _vm.Glance.ResultsReplaced -= OnResultsReplaced;
            _vm.Glance.AddedToTrackList -= OnAddedToTrackList;
        }
        _vm = DataContext as MainViewModel;
        if (_vm is null) return;
        _vm.Glance.PropertyChanged += OnGlanceChanged;
        _vm.Glance.SelectAllOnOpen += OnSelectAllOnOpen;
        _vm.Glance.ResultsReplaced += OnResultsReplaced;
        _vm.Glance.AddedToTrackList += OnAddedToTrackList;
        // Chips slide in only when motion is wanted; the styles key off this class.
        Classes.Set("motion", _vm.Glance.AnimateResults);
        FitShimmer.Animated = _vm.Glance.AnimateResults;
        SyncRows();
        _rail.Sync(_vm.Glance);
        SyncZone();
        SyncChips();
        SyncReassessing();
        SyncCompletion();
        SyncClearHint();
    }

    // ---- View model → view ---------------------------------------------------------------------------

    private void OnGlanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IGlanceViewModel.Rows): SyncRows(); break;
            case nameof(IGlanceViewModel.TableIndex): SyncTableHighlight(); break;
            case nameof(IGlanceViewModel.RailItems): if (Glance is { } g) _rail.Sync(g); break;
            case nameof(IGlanceViewModel.RailIndex): if (Glance is { } h) _rail.SyncHighlight(h); break;
            case nameof(IGlanceViewModel.Zone): SyncZone(); break;
            case nameof(IGlanceViewModel.Chips): SyncChips(); break;
            case nameof(IGlanceViewModel.IsReassessing): SyncReassessing(); break;
            case nameof(IGlanceViewModel.ScopeEmptyText): SyncEmpty(); break;
            case nameof(IGlanceViewModel.ClearArmedText): SyncClearHint(); break;
            case nameof(IGlanceViewModel.CompletionSuffix):
            case nameof(IGlanceViewModel.Query): SyncCompletion(); break;
        }
    }

    /// <summary>The faint tag completion after the typed text, and the footer's Tab caption that goes with it.</summary>
    private void SyncCompletion()
    {
        var suffix = Glance?.CompletionSuffix;
        var atEnd = _keys.CaretAtEndNoSelection();
        GhostTyped.Text = QueryBox.Text ?? "";
        GhostSuffix.Text = suffix ?? "";
        CompletionGhost.IsVisible = !string.IsNullOrEmpty(suffix) && atEnd;
        TabHintText.Text = suffix is not null && atEnd ? "Add tag" : "Rail / Table";
    }



    /// <summary>The footer's Ctrl Del caption turns into the second-press prompt while a clear is armed.</summary>
    private void SyncClearHint()
    {
        var armed = Glance?.ClearArmedText;
        ClearHintText.Text = armed ?? "Clear Track List";
        ClearHintText.Foreground = armed is null
            ? (IBrush?)this.FindResource("SholtoTextMuted")
            : (IBrush?)this.FindResource("SholtoStatusWarn");
    }

    private void OnSelectAllOnOpen()
    {
        Dispatcher.UIThread.Post(() =>
        {
            QueryBox.Focus();
            QueryBox.SelectAll();
        });
    }

    private void SyncRows()
    {
        if (Glance is not { } glance) return;
        _syncing = true;
        try
        {
            ResultsList.ItemsSource = glance.Rows;
            FitShimmer.RowCount = glance.Rows.Count;
            SyncEmpty();
            ResultsList.SelectedIndex = glance.Rows.Count == 0 ? -1 : glance.TableIndex;
        }
        finally { _syncing = false; }
        ScrollTableToHighlight();
    }

    /// <summary>With chips and no rows, one centred line from the view model; otherwise the usual no-match hint.</summary>
    private void SyncEmpty()
    {
        if (Glance is not { } glance) return;
        var none = glance.Rows.Count == 0;
        var scoped = glance.ScopeEmptyText;
        EmptyHint.IsVisible = none && scoped is null;
        ScopeEmptyHint.IsVisible = none && scoped is not null;
        if (!ScopeEmptyHint.IsVisible) return;

        // The names in bright text: every chip's name and the quoted free text.
        var names = new List<string>();
        foreach (var chip in glance.Chips) names.Add(chip.Name);
        var open = scoped!.IndexOf('“');
        var close = open < 0 ? -1 : scoped.IndexOf('”', open + 1);
        if (open >= 0 && close > open) names.Add(scoped.Substring(open + 1, close - open - 1));

        ScopeEmptyHint.Inlines ??= new InlineCollection();
        ScopeEmptyHint.Inlines.Clear();
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
            if (next < 0) { ScopeEmptyHint.Inlines.Add(new Run(scoped[at..])); break; }
            if (next > at) ScopeEmptyHint.Inlines.Add(new Run(scoped[at..next]));
            var bright = new Run(scoped.Substring(next, length)) { FontWeight = FontWeight.SemiBold };
            bright.Bind(TextElement.ForegroundProperty, this.GetResourceObservable("SholtoTextBright"));
            ScopeEmptyHint.Inlines.Add(bright);
            at = next + length;
        }
    }

    /// <summary>The chips changed: the box's watermark and the Backspace hint follow, and focus stays on the box.</summary>
    private void SyncChips()
    {
        if (Glance is not { } glance) return;
        QueryBox.Watermark = glance.Chips.Count > 0 ? ChipWatermark : DefaultWatermark;
        RemoveChipHint.IsVisible = glance.Chips.Count > 0;
        SyncEmpty();
    }

    /// <summary>The slow indicator: rows dim, the real bars hide and the fit column's own neutral bars carry the
    /// light. Under reduced motion the control draws the neutral bars alone.</summary>
    private void SyncReassessing()
    {
        if (Glance is not { } glance) return;
        ResultsList.Classes.Set("reassessing", glance.IsReassessing);
        FitShimmer.IsVisible = glance.IsReassessing;
        FitShimmer.Classes.Set("running", glance.IsReassessing && glance.AnimateResults);
    }

    /// <summary>A result replaced the rows: the table fades in for a moment.</summary>
    private void OnResultsReplaced()
    {
        if (Glance is not { AnimateResults: true }) return;
        // Off then on, so a result landing mid-settle restarts it.
        ResultsList.Classes.Set("fresh", false);
        ResultsList.Classes.Set("fresh", true);
        _settleTimer.Stop();
        _settleTimer.Start();
    }

    private void OnResultsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll) FitShimmer.ScrollOffset = scroll.Offset.Y;
    }

    private void SyncTableHighlight()
    {
        if (Glance is not { } glance) return;
        _syncing = true;
        try { ResultsList.SelectedIndex = glance.Rows.Count == 0 ? -1 : glance.TableIndex; }
        finally { _syncing = false; }
        ScrollTableToHighlight();
    }

    private void ScrollTableToHighlight()
    {
        if (Glance is { } glance && glance.TableIndex >= 0 && glance.TableIndex < glance.Rows.Count)
            ResultsList.ScrollIntoView(glance.TableIndex);
    }

    /// <summary>The list that is not driven right now is dimmed: its highlight turns neutral.</summary>
    private void SyncZone()
    {
        if (Glance is not { } glance) return;
        ResultsList.Classes.Set("dim", glance.Zone != GlanceZone.Table);
        RailList.Classes.Set("dim", glance.Zone != GlanceZone.Rail);
    }

    // ---- Pointer -------------------------------------------------------------------------------------

    /// <summary>Backdrop click → close. The panel swallows its own clicks (<see cref="OnPanelPressed"/>).</summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        Glance?.Close();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    /// <summary>A click on a deck slot loads the highlighted song there; the Glance view model aims at that deck and
    /// goes through the same replace-confirm as Shift+1 / Shift+2.</summary>
    private void OnSlotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IDeckSlotViewModel slot }) Glance?.LoadTo(slot.Number - 1);
        QueryBox.Focus();
    }

    /// <summary>A click on a table row moves the view model's highlight there (the list's own selection
    /// already moved); a click while the rail was driving hands the keys back to the table.</summary>
    private void OnResultsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Glance is not { } glance) return;
        var picked = ResultsList.SelectedIndex;
        if (picked >= 0)
        {
            if (glance.Zone != GlanceZone.Table) glance.ToggleZone();
            glance.Move(picked - glance.TableIndex);
        }
        QueryBox.Focus();
    }

    private void OnRailSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Glance is { } glance && _rail.Pick(glance)) QueryBox.Focus();
    }

    private void OnResultActivated(object? sender, TappedEventArgs e)
    {
        Glance?.Activate();
        e.Handled = true;
    }

    /// <summary>A click on a rail crate or tag adds its chip, or takes it off when it is there. The click's first press
    /// only: a double click is not two toggles. Done after the list has moved its own selection.</summary>
    private void OnRailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 1 || !e.GetCurrentPoint(RailList).Properties.IsLeftButtonPressed) return;
        if (RailItemAt(e.Source) is not (GlanceRailCrate or GlanceRailTag)) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (Glance is not { } glance) return;
            if (glance.Zone != GlanceZone.Rail) glance.ToggleZone();
            glance.ActivateRailItem();
            QueryBox.Focus();
        });
    }

    /// <summary>The rail item under a pointer event's source, or null.</summary>
    private object? RailItemAt(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext;

    /// <summary>The × on a chip.</summary>
    private void OnChipRemoved(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: GlanceChip chip }) Glance?.RemoveChip(chip);
        QueryBox.Focus();
        e.Handled = true;
    }

    /// <summary>A song just joined the Track List: its row, if it is on screen, glows violet for a moment and fades.
    /// Never under reduced motion.</summary>
    private void OnAddedToTrackList(string path)
    {
        if (Glance is not { AnimateResults: true } glance) return;
        for (var i = 0; i < glance.Rows.Count; i++)
        {
            if (glance.Rows[i].FilePath != path) continue;
            if (ResultsList.ContainerFromIndex(i) is not ListBoxItem item) return;
            // Off then on, so a second add mid-flash restarts it.
            item.Classes.Set("flash", false);
            item.Classes.Set("flash", true);
            DispatcherTimer.RunOnce(() => item.Classes.Set("flash", false), TimeSpan.FromMilliseconds(FlashMs));
            return;
        }
    }

    /// <summary>The star cell of a row: highlight that row, then add it to the Track List or remove it.</summary>
    private void OnStarTapped(object? sender, TappedEventArgs e)
    {
        if (Glance is not { } glance || sender is not Control { DataContext: GlanceRow row }) return;
        var index = -1;
        for (var i = 0; i < glance.Rows.Count; i++)
            if (ReferenceEquals(glance.Rows[i], row)) { index = i; break; }
        if (index < 0) return;
        if (glance.Zone != GlanceZone.Table) glance.ToggleZone();
        glance.Move(index - glance.TableIndex);
        glance.ToggleHighlightedInTrackList();
        e.Handled = true;
        QueryBox.Focus();
    }
}
