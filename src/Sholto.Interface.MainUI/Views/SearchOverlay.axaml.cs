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
    private const string DefaultWatermark = "Name, initials (bsn), bpm:128, key:8A, #tag";
    private const string ChipWatermark = "Filter further…";

    private MainViewModel? _vm;
    private readonly ISearchOverlayKeyHandler _keys;
    private readonly ISearchOverlayRailSync _rail;
    private readonly ISearchOverlayResultsSync _results;

    public SearchOverlay()
    {
        InitializeComponent();
        // Forced: XAML constructs this control, and the named controls only exist after InitializeComponent (as WaveformControl).
        _keys = new SearchOverlayKeyHandler(QueryBox);
        _rail = new SearchOverlayRailSync(RailList);
        _results = new SearchOverlayResultsSync(ResultsList, FitShimmer, EmptyHint, ScopeEmptyHint);
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
            _vm.Glance.AddedToTrackList -= OnAddedToTrackList;
        }
        _vm = DataContext as MainViewModel;
        if (_vm is null) return;
        _vm.Glance.PropertyChanged += OnGlanceChanged;
        _vm.Glance.SelectAllOnOpen += OnSelectAllOnOpen;
        _vm.Glance.AddedToTrackList += OnAddedToTrackList;
        // Chips slide in only when motion is wanted; the styles key off this class.
        Classes.Set("motion", _vm.Glance.AnimateResults);
        FitShimmer.Animated = _vm.Glance.AnimateResults;
        var glance = _vm.Glance;
        _results.Sync(glance);
        _rail.Sync(glance);
        SyncZone();
        SyncChips();
        _results.SyncReassessing(glance);
        SyncCompletion();
        SyncClearHint();
    }

    // ---- View model → view ---------------------------------------------------------------------------

    private void OnGlanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Glance is { } g)
        {
            switch (e.PropertyName)
            {
                case nameof(IGlanceViewModel.Rows): _results.Sync(g); break;
                case nameof(IGlanceViewModel.TableIndex): _results.SyncHighlight(g); break;
                case nameof(IGlanceViewModel.RailItems): _rail.Sync(g); break;
                case nameof(IGlanceViewModel.RailIndex): _rail.SyncHighlight(g); break;
                case nameof(IGlanceViewModel.Zone): SyncZone(); break;
                case nameof(IGlanceViewModel.Chips): SyncChips(); break;
                case nameof(IGlanceViewModel.IsReassessing): _results.SyncReassessing(g); break;
                case nameof(IGlanceViewModel.ScopeEmptyText): _results.SyncEmpty(g); break;
            }
        }
        switch (e.PropertyName)
        {
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

    /// <summary>The chips changed: the box's watermark and the Backspace hint follow, and focus stays on the box.</summary>
    private void SyncChips()
    {
        if (Glance is not { } glance) return;
        QueryBox.Watermark = glance.Chips.Count > 0 ? ChipWatermark : DefaultWatermark;
        RemoveChipHint.IsVisible = glance.Chips.Count > 0;
        _results.SyncEmpty(glance);
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
        if (Glance is { } glance && _results.Pick(glance)) QueryBox.Focus();
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

    private void OnAddedToTrackList(string path)
    {
        if (Glance is { } g) _results.Flash(g, path);
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
