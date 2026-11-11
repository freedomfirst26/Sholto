using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sholto.Interface.Faceplate.Model;
using Sholto.Interface.Faceplate.ViewModels;

namespace Sholto.Interface.Faceplate.Views;

/// <summary>The controller guide overlay. Hosts a device drawing, paints the colour
/// language over it, and answers hover and click.
/// <para><b>Amber marks what Sholto uses.</b> Four states, one colour, rising
/// intensity: a control the app acts on carries a resting amber tint; nothing else
/// carries any colour; hover lifts the tint and blooms outward; a click pulses twice
/// and settles into a held glow that is unmistakably the strongest of the three. There
/// is no legend, deliberately — a coloured control is live, a plain one is not.</para>
/// <para>The drawing is never touched. For every shape carrying a
/// <see cref="DeviceLayout.IdProperty"/> this builds four parallel shapes on an
/// overlay canvas: a wide halo, a tight bloom, a fill tint and a transparent hit
/// target. Glows are blurred <i>rings</i> rather than filled shapes so the light
/// spills outward and the control underneath stays readable.</para></summary>
public partial class FaceplateOverlay : UserControl
{
    // The Faceplate project has no Avalonia name generator, so the named parts of the
    // .axaml are resolved once, here, rather than through generated fields.
    private readonly Grid _stage;
    private readonly ContentControl _layoutHost;
    private readonly Canvas _overlayCanvas;
    private readonly Border _panel;
    private readonly Grid _backdrop;
    private readonly Border _feather;
    private readonly TextBlock _panelDeck;
    private readonly TextBlock _panelLabel;
    private readonly TextBlock _panelSummary;
    private readonly TextBlock _emptyState;
    private readonly StackPanel _headlinePanel;
    private readonly StackPanel _rowsPanel;
    private readonly Border _layerBadge;
    private readonly TextBlock _layerBadgeText;

    private readonly FaceplateViewModel _vm;
    private readonly FaceplateBoard _board;
    private readonly FaceplatePanelContext _panelContext;
    private readonly IFaceplateHeadlineFactory _headlines;
    private readonly IFaceplateRowVisualFactory _rows;
    private readonly IFaceplateChrome _chrome;
    private readonly FaceplateBrushes _brushes;

    // Marks which ROW of the panel a live gesture just fired — as opposed to the
    // pieces above, which mark which CONTROL is lit on the drawing. Rebuilt every
    // time RebuildRows/RebuildHeadline runs, so a fresh selection never carries a
    // stale indicator forward from the control that was showing before it.
    private readonly Dictionary<string, Border> _rowIndicators = new();
    private Border? _headlineIndicator;

    /// <summary>The device drawing, view model, headline and row factories, chrome painter,
    /// shape and step-badge factories and brushes are handed in, already built, so this control names no hardware and
    /// builds none of its collaborators; <c>FaceplateOverlayFactory</c> pairs a doc with its
    /// layout. The view model and brushes are concrete classes; the factories and chrome are interfaces.</summary>
    public FaceplateOverlay(FaceplateViewModel viewModel, IFaceplateHeadlineFactory headlines,
                            IFaceplateRowVisualFactory rows, IFaceplateChrome chrome,
                            IFaceplateShapeFactory shapes, IFaceplateStepBadgeFactory badges,
                            FaceplateBrushes brushes, Control layout)
    {
        AvaloniaXamlLoader.Load(this);
        _stage = this.GetControl<Grid>("Stage");
        _layoutHost = this.GetControl<ContentControl>("LayoutHost");
        _overlayCanvas = this.GetControl<Canvas>("OverlayCanvas");
        _panel = this.GetControl<Border>("Panel");
        _backdrop = this.GetControl<Grid>("Backdrop");
        _feather = this.GetControl<Border>("Feather");
        _panelDeck = this.GetControl<TextBlock>("PanelDeck");
        _panelLabel = this.GetControl<TextBlock>("PanelLabel");
        _panelSummary = this.GetControl<TextBlock>("PanelSummary");
        _headlinePanel = this.GetControl<StackPanel>("HeadlinePanel");
        _emptyState = this.GetControl<TextBlock>("EmptyState");
        _rowsPanel = this.GetControl<StackPanel>("RowsPanel");
        _layerBadge = this.GetControl<Border>("LayerBadge");
        _layerBadgeText = this.GetControl<TextBlock>("LayerBadgeText");
        _vm = viewModel;
        _headlines = headlines;
        _rows = rows;
        _chrome = chrome;
        _brushes = brushes;
        _brushes.Attach(this);
        _board = new FaceplateBoard(_stage, _layoutHost, _overlayCanvas,
            _vm.IsImplemented, () => _vm.SelectedDeck, _vm.Select,
            id => _vm.Doc.Controls.FirstOrDefault(c => c.Id == id)?.Scope == "per-deck",
            _brushes, shapes, badges);
        _panelContext = new FaceplatePanelContext(this, _vm, _board);
        DataContext = _vm;
        _layoutHost.Content = layout;
        // The device layout is drawn in its reference render's own page space
        // (1792x1316) and the unit itself only occupies 1616x908 of that, centred —
        // so a Viewbox fitting the page fits ~15% of empty margin along with it, and
        // the board comes out visibly smaller than the room it has. Crop most of that
        // padding back off here, in the overlay, rather than in the device layout: the
        // layout is the drawing's own coordinate space and every control rectangle in
        // the guide is measured against it. A little bleed is left deliberately, since
        // the bloom rings spill outside a control's own edge.
        _stage.Margin = new Thickness(-72, -170, -72, -170);
        _vm.SelectionChanged += _board.OnSelectionChanged;
        // A live gesture off the controller selects a control the same way a click does
        // (FaceplateViewModel's CommandReceived handler already updated Selected/SelectedDeck before
        // raising this), but announces it separately so it can guard against re-blinking
        // on every tick of a knob that's already selected. Route it through the exact
        // same blink-then-glow OnSelectionChanged uses for a click — not a second
        // animation — rather than duplicating BlinkThenSelect here.
        _vm.BlinkRequested += id => _board.OnSelectionChanged(id, _vm.SelectedDeck);
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(FaceplateViewModel.IsPanelOpen):
                    SyncPanel();
                    break;
                case nameof(FaceplateViewModel.Title):
                    ApplyTitle();
                    break;
                case nameof(FaceplateViewModel.Summary):
                    _panelSummary.Text = _vm.Summary;
                    break;
                case nameof(FaceplateViewModel.Headline):
                    RebuildHeadline();
                    break;
                case nameof(FaceplateViewModel.Rows):
                    RebuildRows();
                    break;
                case nameof(FaceplateViewModel.Highlighted):
                    _board.ApplyHighlighted(_vm.Highlighted);
                    break;
                case nameof(FaceplateViewModel.ActiveRowId):
                    ApplyActiveRow();
                    break;
                case nameof(FaceplateViewModel.ActiveLayerId):
                    ApplyLayerBadge();
                    break;
            }
        };
        // Bounds are only real after a layout pass, and the overlay rectangles are
        // measured off the drawing rather than assumed, so the whole device layout can
        // be re-drawn without touching this file.
        LayoutUpdated += OnLayoutUpdated;
        ApplyTitle();
        _panelSummary.Text = _vm.Summary;
        ApplyLayerBadge();
        // The scrim and the feather are raw Colors mixed from the theme, not brushes
        // bound to it, so they have to be repainted whenever the theme changes under
        // the overlay — the app swaps its Sholto* resources live from the theme menu.
        Loaded += (_, _) => ApplyTheme();
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        // …and, the one that actually matters, whenever the host swaps its Sholto*
        // brushes. Sholto changes theme by writing new brushes into Window.Resources,
        // which raises no theme-variant change and no visibility change; without this
        // the scrim and the panel face stayed on whatever palette happened to be live
        // when the overlay first loaded, while every DynamicResource around them moved
        // — a grey panel with magenta chips in it.
        ResourcesChanged += (_, _) => ApplyTheme();
    }

    /// <summary>The title, split in two. "CUE" and "deck 1" are different facts and
    /// belong on different lines: the deck goes above as the title's eyebrow, the name
    /// below in full weight.
    /// <para>A global control has no deck, and then the eyebrow is collapsed rather
    /// than blanked — an empty <c>TextBlock</c> still takes its line height, and the
    /// crossfader's title would have sat a row lower than everything else's.</para>
    /// </summary>
    private void ApplyTitle()
    {
        var title = _vm.Title;
        var cut = title.IndexOf(" · ", StringComparison.Ordinal);
        _panelLabel.Text = cut < 0 ? title : title[..cut];
        // Upper-cased here rather than at the source: the view model's title is prose
        // ("CUE · deck 1") and is read by the tests and by anything else that wants a
        // sentence. Only the eyebrow shouts.
        _panelDeck.Text = cut < 0 ? string.Empty : title[(cut + 3)..].ToUpperInvariant();
        _panelDeck.IsVisible = cut >= 0;
    }

    /// <summary>The view model, so a host can drive selection from live gestures.</summary>
    public FaceplateViewModel ViewModel => _vm;

    /// <summary>Fires when the guide should be dismissed.</summary>
    public event Action? RequestClose
    {
        add => _vm.RequestClose += value;
        remove => _vm.RequestClose -= value;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_board.IsBuilt || _stage.Bounds.Width <= 0) return;
        _board.WireShapes();
        LayoutUpdated -= OnLayoutUpdated;
    }

    // ---- panel -----------------------------------------------------------------

    private void SyncPanel() =>
        // One frame later: the Border has to be visible before the slide can be seen.
        Dispatcher.UIThread.Post(() =>
        {
            if (_vm.IsPanelOpen) _panel.Classes.Add("open");
            else _panel.Classes.Remove("open");
        }, DispatcherPriority.Render);

    /// <summary>Reading the panel — the summary, a bullet's result text, the empty
    /// space below the last bullet — must never be an accidental dismiss. The backdrop
    /// no longer closes on its own click, but marking this handled too means the panel
    /// stays immune even if a future task reintroduces a click-outside policy on the
    /// backdrop; the drawing's own shapes already do the same (see WireShapes).</summary>
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    /// <summary>The visible X, top-right of the overlay frame — one of only two ways to
    /// dismiss the guide, the other being Esc. The backdrop deliberately does NOT close
    /// on a stray click: it's the canvas the drawing floats on, and a DJ aiming at a
    /// small control (a pad, a knob) who misses by a few pixels must not lose the whole
    /// guide and their place in it. Ends at <see cref="FaceplateViewModel.Close"/>, which
    /// is what tells the host to set gesture routing back to Play.</summary>
    private void OnCloseButtonClick(object? sender, RoutedEventArgs e) => _vm.Close();

    // ---- the gesture panel -------------------------------------------------------

    /// <summary>Rebuilds the single headline line: "what does a plain press do?".
    /// Empty (nothing shown) when the control has no single answer to that — either it
    /// owns several plain gestures at once (the jog), or none at all (TRIM).</summary>
    private void RebuildHeadline()
    {
        _headlinePanel.Children.Clear();
        _headlineIndicator = null;
        var headline = _vm.Headline;
        SyncEmptyState();
        if (headline is null) return;

        var section = _headlines.Create(headline, _panelContext);
        foreach (var child in section.Children) _headlinePanel.Children.Add(child);
        _headlineIndicator = section.Indicators[headline.GestureId];
        ApplyActiveRow();
    }

    /// <summary>Sets every row indicator's visibility from
    /// <see cref="FaceplateViewModel.ActiveRowId"/> in one pass. Cheap enough to call
    /// on every gesture and every rebuild rather than tracking a diff — there are at
    /// most a handful of rows open at once.</summary>
    private void ApplyActiveRow()
    {
        var active = _vm.ActiveRowId;
        foreach (var (gestureId, indicator) in _rowIndicators)
            indicator.Opacity = gestureId == active ? 1.0 : 0.0;
        if (_headlineIndicator is not null)
            _headlineIndicator.Opacity = active is not null && active == _vm.Headline?.GestureId ? 1.0 : 0.0;
    }

    private void SyncEmptyState() =>
        _emptyState.IsVisible =
            _vm.Selected is not null && _vm.Headline is null && _vm.Rows.Count == 0;

    /// <summary>Rebuilds the bullet list from <see cref="FaceplateViewModel.Rows"/>:
    /// everything the selected control is involved in besides its headline — its own
    /// remaining gestures, and any gesture owned by another control that names this one
    /// as a partner.</summary>
    private void RebuildRows()
    {
        _rowsPanel.Children.Clear();
        _rowIndicators.Clear();
        // A control with no headline and no bullets still needs to say so — otherwise
        // the header rule under the summary is a divider with nothing under it.
        SyncEmptyState();
        if (_vm.Rows.Count == 0) return;

        var section = _rows.Create(_vm.Rows, _panelContext);
        foreach (var child in section.Children) _rowsPanel.Children.Add(child);
        foreach (var (gestureId, indicator) in section.Indicators) _rowIndicators[gestureId] = indicator;
        ApplyActiveRow();
    }

    /// <summary>The one cheap, honest indication that a modifier is being held: name
    /// it in the panel header. Hidden on "plain" — the board's default state needs no
    /// label, same rule the row tags below apply to their own layer chip.
    /// <para>Deliberately NOT a rebuild of the board's own labels for the held layer:
    /// that would mean re-deriving, per control, what its shape map draws for a layer
    /// other than "plain", and speculatively reworking the drawing's own text to match
    /// — a much larger and riskier change than this task called for. This says which
    /// layer is live and stops there.</para></summary>
    private void ApplyLayerBadge()
    {
        var layerId = _vm.ActiveLayerId;
        var isPlain = layerId == "plain";
        _layerBadge.IsVisible = !isPlain;
        if (!isPlain) _layerBadgeText.Text = _panelContext.LayerNameOf(layerId).ToUpperInvariant();
    }

    private IBrush? _themeStamp;

    /// <summary>Repaints everything in the panel that took a colour by value rather
    /// than by binding — the scrim, the feather, the panel face, and the headline and
    /// bullet text, which are built in code and hold brush references rather than
    /// DynamicResource bindings. <c>ResourcesChanged</c> fires dozens of times during
    /// startup, so the palette's own brush instance is the guard: the theme changed
    /// only if the app swapped that object.</summary>
    private void ApplyTheme()
    {
        var stamp = this.ResourceBrush("SholtoSurfaceRaised", Brushes.Transparent);
        if (ReferenceEquals(stamp, _themeStamp)) return;
        _themeStamp = stamp;
        _chrome.Paint(this, _backdrop, _feather, _panel);
        RebuildHeadline();
        RebuildRows();
    }
}
