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
/// the header chip and deck captions that follow the deck view models, and the motion (the results settling in,
/// the slow indicator, a rail count ticking) that the view model only cues.
/// </summary>
public partial class SearchOverlay : UserControl
{
    private const int PageSize = 8;

    /// <summary>How long the "fresh" class stays on the table after a result: past the 220 ms bar re-grow.</summary>
    private const int SettleHoldMs = 260;

    private const string DefaultWatermark = "Name, initials (bsn), bpm:128, key:8A, #tag";
    private const string ChipWatermark = "Filter further…";

    private MainViewModel? _vm;
    private bool _syncing;
    private bool _swallowNextText;
    private readonly DispatcherTimer _settleTimer;
    private readonly Dictionary<string, int> _railCounts = [];
    private readonly HashSet<string> _tickKeys = [];

    public SearchOverlay()
    {
        InitializeComponent();
        // Tunnel, so the box's own handling of arrows, Tab and Enter never sees these keys.
        QueryBox.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
        QueryBox.AddHandler(TextInputEvent, OnQueryTextInput, RoutingStrategies.Tunnel);
        QueryBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty || e.Property == TextBox.SelectionStartProperty
                || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.TextProperty)
                SyncCompletion();
        };
        // Tunnel: the list selects on press and marks it handled, so a click on a crate or tag is read before that.
        RailList.AddHandler(PointerPressedEvent, OnRailPressed, RoutingStrategies.Tunnel);
        RailList.ContainerPrepared += OnRailContainerPrepared;
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
            _vm.Glance.Header.PropertyChanged -= OnHeaderChanged;
        }
        _vm = DataContext as MainViewModel;
        if (_vm is null) return;
        _vm.Glance.PropertyChanged += OnGlanceChanged;
        _vm.Glance.SelectAllOnOpen += OnSelectAllOnOpen;
        _vm.Glance.ResultsReplaced += OnResultsReplaced;
        _vm.Glance.Header.PropertyChanged += OnHeaderChanged;
        // Chips slide in only when motion is wanted; the styles key off this class.
        Classes.Set("motion", _vm.Glance.AnimateResults);
        FitShimmer.Animated = _vm.Glance.AnimateResults;
        SyncRows();
        SyncRail();
        SyncZone();
        SyncChips();
        SyncReassessing();
        SyncHeaderChip();
        SyncCompletion();
    }

    // ---- View model → view ---------------------------------------------------------------------------

    private void OnGlanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IGlanceViewModel.Rows): SyncRows(); break;
            case nameof(IGlanceViewModel.TableIndex): SyncTableHighlight(); break;
            case nameof(IGlanceViewModel.RailItems): SyncRail(); break;
            case nameof(IGlanceViewModel.RailIndex): SyncRailHighlight(); break;
            case nameof(IGlanceViewModel.Zone): SyncZone(); break;
            case nameof(IGlanceViewModel.Chips): SyncChips(); break;
            case nameof(IGlanceViewModel.IsReassessing): SyncReassessing(); break;
            case nameof(IGlanceViewModel.ScopeEmptyText): SyncEmpty(); break;
            case nameof(IGlanceViewModel.CompletionSuffix):
            case nameof(IGlanceViewModel.Query): SyncCompletion(); break;
        }
    }

    private bool CaretAtEndNoSelection()
    {
        var length = QueryBox.Text?.Length ?? 0;
        return QueryBox.CaretIndex == length && QueryBox.SelectionStart == QueryBox.SelectionEnd;
    }

    /// <summary>The faint tag completion after the typed text, and the footer's Tab caption that goes with it.</summary>
    private void SyncCompletion()
    {
        var suffix = Glance?.CompletionSuffix;
        var atEnd = CaretAtEndNoSelection();
        GhostTyped.Text = QueryBox.Text ?? "";
        GhostSuffix.Text = suffix ?? "";
        CompletionGhost.IsVisible = !string.IsNullOrEmpty(suffix) && atEnd;
        TabHintText.Text = suffix is not null && atEnd ? "Add tag" : "Rail / Table";
    }

    private void OnHeaderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IGlanceHeaderViewModel.ReferenceDeck) or nameof(IGlanceHeaderViewModel.KeyText))
            SyncHeaderChip();
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

    /// <summary>A result replaced the rows: the table settles in (and the bars re-grow) for a moment.</summary>
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

    private void SyncRail()
    {
        if (Glance is not { } glance) return;
        _syncing = true;
        try
        {
            MarkChangedRailCounts(glance);
            RailList.ItemsSource = glance.RailItems;
            RailList.SelectedIndex = glance.RailIndex;
        }
        finally { _syncing = false; }
        ScrollRailToHighlight();
    }

    /// <summary>Which crates and tags show a different count than last time: their rows tick as they are built.</summary>
    private void MarkChangedRailCounts(IGlanceViewModel glance)
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

    private void OnRailContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem box) return;
        var (key, _) = RailCount(box.DataContext ?? RailList.Items[e.Index]!);
        // Containers are recycled, so the class is set either way. Each key ticks once.
        box.Classes.Set("tick", key is not null && _tickKeys.Remove(key));
    }

    private void SyncRailHighlight()
    {
        if (Glance is not { } glance) return;
        _syncing = true;
        try { RailList.SelectedIndex = glance.RailIndex; }
        finally { _syncing = false; }
        ScrollRailToHighlight();
    }

    private void ScrollRailToHighlight()
    {
        if (Glance is { } glance && glance.RailIndex >= 0 && glance.RailIndex < glance.RailItems.Count)
            RailList.ScrollIntoView(glance.RailIndex);
    }

    /// <summary>The list that is not driven right now is dimmed: its highlight turns neutral.</summary>
    private void SyncZone()
    {
        if (Glance is not { } glance) return;
        ResultsList.Classes.Set("dim", glance.Zone != GlanceZone.Table);
        RailList.Classes.Set("dim", glance.Zone != GlanceZone.Rail);
    }

    /// <summary>The header's key chip takes the colour the reference deck's own chip has.</summary>
    private void SyncHeaderChip()
    {
        if (_vm is null) return;
        var deck = _vm.Glance.Header.ReferenceDeck switch { 0 => _vm.Deck1, 1 => _vm.Deck2, _ => null };
        if (deck is null) return;
        HeaderKeyChip.Bind(Border.BackgroundProperty, new Binding(nameof(DeckViewModel.KeyBrush)) { Source = deck });
    }

    // ---- Keys ----------------------------------------------------------------------------------------

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        // A swallow set by the previous key never outlives it, whether or not text input followed.
        _swallowNextText = false;
        if (Glance is not { } glance) return;

        if (e.KeyModifiers == KeyModifiers.Shift && e.Key is Key.D1 or Key.NumPad1 or Key.D2 or Key.NumPad2)
        {
            // The character this key would type ("!", "@", or a layout's own) must not reach the box.
            _swallowNextText = true;
            glance.LoadTo(e.Key is Key.D1 or Key.NumPad1 ? 0 : 1);
            e.Handled = true;
            return;
        }
        // Ctrl+Enter on a rail crate or tag filters the library and closes; before the modifier return below.
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Enter)
        {
            glance.ActivateAlternate();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers != KeyModifiers.None) return;

        switch (e.Key)
        {
            case Key.Escape: glance.Close(); break;
            case Key.Down: glance.Move(+1); break;
            case Key.Up: glance.Move(-1); break;
            case Key.PageDown: glance.Move(+PageSize); break;
            case Key.PageUp: glance.Move(-PageSize); break;
            case Key.Tab:
                if (CaretAtEndNoSelection() && glance.AcceptTagCompletion())
                    Dispatcher.UIThread.Post(() => QueryBox.CaretIndex = QueryBox.Text?.Length ?? 0);
                else
                    glance.ToggleZone();
                break;
            case Key.Left:
            case Key.Right: glance.FlipTarget(); break;
            case Key.Enter: glance.Activate(); break;
            // In an empty box Backspace takes the newest chip off; with text or no chip it edits as usual.
            case Key.Back:
                if (!glance.RemoveLastChip()) return;
                break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnQueryTextInput(object? sender, TextInputEventArgs e)
    {
        if (_swallowNextText)
        {
            _swallowNextText = false;
            e.Handled = true;
            return;
        }
        // Q shortlists only while the box is empty; otherwise the view model says no and it types.
        if (e.Text is "q" or "Q" && Glance is { } glance && glance.TryShortlistKey())
            e.Handled = true;
    }

    // ---- Pointer -------------------------------------------------------------------------------------

    /// <summary>Backdrop click → close. The panel swallows its own clicks (<see cref="OnPanelPressed"/>).</summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        Glance?.Close();
        e.Handled = true;
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnTargetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: IDeckSlotViewModel slot }) Glance?.SetTarget(slot.Number - 1);
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
        if (_syncing || Glance is not { } glance) return;
        var picked = RailList.SelectedIndex;
        if (picked >= 0 && glance.RailItems[picked] is not GlanceRailHeader)
        {
            if (glance.Zone != GlanceZone.Rail) glance.ToggleZone();
            glance.Move(SelectableSteps(glance.RailItems, glance.RailIndex, picked));
        }
        else
        {
            // A header cannot be highlighted; put the list's selection back where the view model has it.
            SyncRailHighlight();
        }
        QueryBox.Focus();
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

    /// <summary>Double click on a rail track loads it; crates and tags already acted on the first press.</summary>
    private void OnRailDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RailItemAt(e.Source) is not GlanceRailTrack) return;
        Glance?.Activate();
        e.Handled = true;
    }

    /// <summary>The rail item under a pointer event's source, or null.</summary>
    private object? RailItemAt(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext;

    /// <summary>The × on a chip.</summary>
    private void OnChipRemovePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: GlanceChip chip }) Glance?.RemoveChip(chip);
        QueryBox.Focus();
        e.Handled = true;
    }

    /// <summary>The star cell of a row: highlight that row, then toggle its shortlist mark.</summary>
    private void OnStarTapped(object? sender, TappedEventArgs e)
    {
        if (Glance is not { } glance || sender is not Control { DataContext: GlanceRow row }) return;
        var index = -1;
        for (var i = 0; i < glance.Rows.Count; i++)
            if (ReferenceEquals(glance.Rows[i], row)) { index = i; break; }
        if (index < 0) return;
        if (glance.Zone != GlanceZone.Table) glance.ToggleZone();
        glance.Move(index - glance.TableIndex);
        glance.ToggleShortlistOnHighlight();
        e.Handled = true;
        QueryBox.Focus();
    }
}
