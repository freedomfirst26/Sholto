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
using Sholto.Faceplate.Devices.DdjFlx4;
using Sholto.Faceplate.Model;
using Sholto.Faceplate.ViewModels;

namespace Sholto.Faceplate.Views;

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

    // Marks which ROW of the panel a live gesture just fired — as opposed to the
    // pieces above, which mark which CONTROL is lit on the drawing. Rebuilt every
    // time RebuildRows/RebuildHeadline runs, so a fresh selection never carries a
    // stale indicator forward from the control that was showing before it.
    private readonly Dictionary<string, Border> _rowIndicators = new();
    private Border? _headlineIndicator;

    /// <summary>Parameterless constructor required by Avalonia's XAML object
    /// factory (<c>&lt;fp:FaceplateOverlay/&gt;</c> in MainWindow.axaml) — the XAML
    /// loader instantiates controls with no constructor arguments and no
    /// injection point, the same constraint that keeps <c>StyledProperty</c>
    /// registrations static. <see cref="FaceplateDocLoader"/> has nothing to be
    /// composed from here, so this builds a throwaway one rather than not working
    /// at all from markup; the real, testable path is the
    /// <see cref="FaceplateOverlay(FaceplateDoc)"/> constructor below, which takes
    /// already-loaded data.</summary>
    public FaceplateOverlay() : this(new FaceplateDocLoader().LoadEmbedded("ddj-flx4")) { }

    public FaceplateOverlay(FaceplateDoc doc)
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
        _vm = new FaceplateViewModel(doc);
        _board = new FaceplateBoard(_stage, _layoutHost, _overlayCanvas,
            _vm.IsImplemented, () => _vm.SelectedDeck, _vm.Select,
            id => _vm.Doc.Controls.FirstOrDefault(c => c.Id == id)?.Scope == "per-deck");
        DataContext = _vm;
        _layoutHost.Content = new DdjFlx4Layout();
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
        // (FaceplateViewModel.OnLiveGesture already updated Selected/SelectedDeck before
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

        _headlinePanel.Children.Add(SectionLabel($"IF YOU JUST {headline.Verb.ToUpperInvariant()} IT"));

        // A plain press that does nothing (transport CUE, BEAT SYNC) says so here,
        // plainly — that is the honest and useful answer, not a reason to hide it. It
        // is said the way the DRAWING says it, by withholding rather than by warning:
        // the board gives an unimplemented control no colour at all, so the panel gives
        // the sentence no weight — one grey step down, italic, no accent and no red.
        // Italic carries exactly one meaning anywhere in this panel, "this is a
        // negative statement", which is what makes the state findable in a glance
        // without a colour shouting at anyone.
        var head = BuildProseText(headline.Result, 16, FontWeight.SemiBold,
            this.ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 22);
        if (!headline.Used)
        {
            // Same 16px as a working answer, deliberately. The size says "this is the
            // answer to the question the eyebrow just asked"; the italic and the 0.62
            // say the answer is a negative. Dropping the size as well made the negative
            // headline merge with the summary above it into one continuous grey slab,
            // and the reader could no longer tell which of the two was the answer.
            head.FontWeight = FontWeight.Normal;
            head.FontStyle = FontStyle.Italic;
            head.Opacity = 0.62;
        }
        _headlineIndicator = RowIndicator();
        _headlinePanel.Children.Add(WithRowIndicator(_headlineIndicator, head));
        ApplyActiveRow();
    }

    /// <summary>The thin accent bar that marks "this is the row a live gesture just
    /// fired" — the same accent colour the row tags and the new layer badge wear
    /// (never amber; amber is the drawing's alone). Built invisible and toggled by
    /// <see cref="ApplyActiveRow"/>, not rebuilt per gesture: a knob can fire many
    /// times a second and only the opacity needs to move.</summary>
    private Border RowIndicator() => new()
    {
        Width = 3,
        CornerRadius = new CornerRadius(2),
        Margin = new Thickness(0, 1, 8, 1),
        Background = this.ResourceBrush("SholtoAccent", Brushes.Orange),
        Opacity = 0,
        IsHitTestVisible = false,
    };

    /// <summary>Lays the indicator bar and a row's own content side by side without
    /// disturbing that content's existing hover/click wiring — the indicator is a
    /// sibling, not a wrapper around it.</summary>
    private static Grid WithRowIndicator(Border indicator, Control content)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(indicator, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(indicator);
        grid.Children.Add(content);
        return grid;
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

    /// <summary>The small all-caps eyebrow over a section. Carries no styling of its
    /// own: size, weight, tracking, opacity and colour all come from the single
    /// <c>TextBlock.eyebrow</c> style in the .axaml, which the deck eyebrow above the
    /// title wears too. See that style for why the colour is what it is.</summary>
    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        Classes = { "eyebrow" },
    };

    /// <summary>A 1px full-width rule in the theme's border colour. Two weights: 0.55
    /// for the header rule under the summary (the same device
    /// <c>TagEditorView.axaml</c> uses), 0.45 for the quieter rules between bullets, so
    /// a list separator never reads as a section break.</summary>
    private Border Hairline(double opacity, Thickness margin) => new()
    {
        Height = 1,
        Opacity = opacity,
        Margin = margin,
        Background = this.ResourceBrush("SholtoBorder", Brushes.DimGray),
        IsHitTestVisible = false,
    };

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

        var label = SectionLabel("EVERYTHING ELSE IT DOES");
        // 10 either side, matching the row card's padding, so the eyebrow and the
        // hairlines line up with the bullet text they belong to rather than with the
        // card's outer edge (see BuildRowVisual for why the gutter is split).
        label.Margin = new Thickness(RowGutter, 0, RowGutter, 3);
        _rowsPanel.Children.Add(label);

        var first = true;
        foreach (var row in _vm.Rows)
        {
            // A rule between bullets, never above the first one — the section label is
            // already the boundary there, and a rule under it would fence the label off
            // from the list it belongs to.
            if (!first) _rowsPanel.Children.Add(Hairline(0.45, new Thickness(RowGutter, 14, RowGutter, 14)));
            _rowsPanel.Children.Add(BuildRowVisual(row));
            first = false;
        }
        ApplyActiveRow();
    }

    private Control BuildRowVisual(GestureRow row)
    {
        // A chord names every participant — "SHIFT + Jog wheel" — as a chip per
        // control with a plain "+" between them, so the reader never has to guess
        // which control this bullet is really about, whether it is this control's own
        // gesture or one it merely takes part in. Built from the gesture's own With
        // list plus its owner (ComboMarkers), never by splitting a rendered string on
        // "+". A plain gesture (no partner) falls back to verb + part, as before —
        // that phrase names no control, so it stays plain text.
        var nameForeground = this.ResourceBrush("SholtoTextBright", Brushes.White);
        Control nameControl = row.Partners.Count > 0
            ? BuildProseText(ProseWithChips.ComboMarkers(row.Partners, row.OwnerControlId),
                              13, FontWeight.SemiBold, nameForeground, lineHeight: 18)
            : new TextBlock
              {
                  Text = row.Part is null ? Capitalize(row.Verb) : $"{Capitalize(row.Verb)} — {row.Part}",
                  FontSize = 13,
                  FontWeight = FontWeight.SemiBold,
                  LineHeight = 18,
                  TextWrapping = TextWrapping.Wrap,
                  Foreground = nameForeground,
              };
        ((Control)nameControl).Opacity = 0.86;

        var stack = new StackPanel();
        // A control with two rows on the same layer (a pad, across pages) has nothing
        // else to tell them apart by, since a page switch names no partner — so a
        // non-plain layer is tagged. "Plain" itself gets NO tag: everything is plain
        // unless something overrides it, so a PLAIN label on most rows would be noise
        // that dilutes the tags that actually mean something (SHIFT, HOT CUE page, …).
        // Skipping the child entirely — not just hiding it — is what keeps the
        // untagged rows from showing a gap where the tag would have sat.
        // The tag now wears the app's own chip form (TagEditorView) rather than floating
        // as a coloured word: a filled pill in SholtoAccentBg, which is the same brush
        // the track list, the search overlay and the crate picker all use for "this one
        // is picked". It is the only colour in the panel, and it only appears when
        // there is a modifier to name.
        // …but not when the chord above it already names the modifier: a bullet reading
        // SHIFT / "SHIFT + JOG WHEEL" says the same word twice in two different forms
        // one line apart, and the reader has to check whether the second one means
        // something new. The tag stays for a layered gesture whose modifier is not in
        // the chord — the pad pages, where the page switch names no partner.
        var modifier = _vm.Doc.Layers.FirstOrDefault(l => l.Id == row.Layer)?.Modifier;
        if (row.Layer != "plain" && (modifier is null || !row.Partners.Contains(modifier)))
        {
            stack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 1, 7, 2),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = this.ResourceBrush("SholtoAccentBg", Brushes.Transparent),
                Child = new TextBlock
                {
                    Text = LayerNameOf(row.Layer).ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    LetterSpacing = 0.8,
                    LineHeight = 13,
                    Foreground = this.ResourceBrush("SholtoAccent", Brushes.Orange),
                },
            });
        }
        stack.Children.Add(nameControl);

        // Same 13px as the gesture name above it. Weight and opacity do the separating,
        // which keeps the whole panel on four sizes: 18 title, 16 answer, 13 body,
        // 11 eyebrow and cap.
        var result = BuildProseText(row.Result, 13, FontWeight.Normal,
            this.ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 18);
        // 6, not 4: the gesture name above is 13px on an 18px line, so 4 put the result
        // closer to its own name than the name's two wrapped lines are to each other and
        // the pair read as one squashed block. A name carrying chips sits on a 27px
        // line, which already has more air under it, so the gap is taken off there to
        // keep the name→result step the same distance in both kinds of bullet.
        result.Margin = new Thickness(0, nameControl is TextBlock { LineHeight: > 18 } ? 2 : 6, 0, 0);
        result.Opacity = 0.56;
        if (!row.Used)
        {
            // Same rule as the headline: no red, no accent — italic and a grey step
            // down, which is the panel's one and only way of saying "not this one".
            result.FontStyle = FontStyle.Italic;
            result.Foreground = this.ResourceBrush("SholtoTextMuted", Brushes.Gray);
            result.Opacity = 0.70;
        }
        stack.Children.Add(result);

        // At rest a bullet has no chrome at all — nine bordered boxes in a column is
        // what made this read as a bolted-on document viewer rather than part of
        // Sholto, and the hairline rhythm above turns those nine objects into one
        // list. The box moves to hover, where it MEANS something: it appears exactly
        // when the pointer is on a row, which is exactly when the matching controls
        // light up on the board.
        // The card sits INSIDE the panel's gutter, not across it. It used to carry a
        // -10 horizontal margin meant to bleed into that gutter, but the ScrollViewer
        // clips to its viewport, so the bleed was simply cut off: the highlight ended
        // flush with the first letter of its own text on one side and with the panel's
        // border on the other, its rounded corners sliced away, reading as a block
        // bleeding off the edge rather than a card lifting off the page. The gutter is
        // now split — 12 of panel margin outside the card, 10 of padding inside it —
        // so nothing is clipped and the text keeps the same 22 the title and summary
        // sit on (see the ScrollViewer's margin in FaceplateOverlay.axaml).
        var border = new Border
        {
            Padding = new Thickness(RowGutter, 8),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = stack,
        };
        border.Transitions = new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.BrushTransition
            {
                Property = Border.BackgroundProperty,
                Duration = TimeSpan.FromMilliseconds(120),
            },
        };
        border.PointerEntered += (_, _) =>
        {
            border.Background = this.ResourceBrush("SholtoAccentBg", Brushes.Transparent);
            _vm.HoverRow(row.GestureId);
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            _vm.HoverRow(null);
        };

        var indicator = RowIndicator();
        _rowIndicators[row.GestureId] = indicator;
        return WithRowIndicator(indicator, border);
    }

    /// <summary>Builds a prose <see cref="TextBlock"/> from marker text, wiring every
    /// chip it contains to the same hover and selection paths a drawing shape uses:
    /// hover lights the named control, a click selects it exactly as clicking its
    /// shape would (see <see cref="ChipHoverOn"/>/<see cref="ChipActivate"/>).</summary>
    private TextBlock BuildProseText(string markerText, double fontSize, FontWeight weight,
                                     IBrush foreground, double lineHeight)
    {
        var tb = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground,
        };
        tb.Inlines = ProseWithChips.Build(markerText, _vm.Doc, ChipDeck,
                                          ChipHoverOn, ChipHoverOff, ChipActivate);
        // A chip is a bordered, padded box roughly 19px tall, so a line height chosen
        // for 13px or 16px TEXT is shorter than the line it now has to hold. Avalonia
        // does not grow the line to fit: it lays the lines out at the height it was
        // given and the chips spill into their neighbours, which is what made a
        // chip-bearing sentence read as two sentences printed on top of each other.
        // Prose with chips therefore gets a line tall enough for the cap, and prose
        // without keeps the tighter rhythm it was tuned for.
        tb.LineHeight = tb.Inlines.OfType<InlineUIContainer>().Any()
            ? Math.Max(lineHeight, ChipLineHeight)
            : lineHeight;
        return tb;
    }

    /// <summary>The padding inside a bullet's hover card, and equally the margin the
    /// section eyebrow and the hairlines carry so everything in the list starts on the
    /// same left edge as the bullet text.</summary>
    private const double RowGutter = 10;

    /// <summary>The line height a paragraph needs once it contains a key cap: the cap's
    /// own height plus the leading that keeps a wrapped line from touching it.</summary>
    private const double ChipLineHeight = 24;

    /// <summary>Which deck a per-deck control's chip should point at.
    /// <para><see cref="FaceplateViewModel.SelectedDeck"/> alone is wrong: it is -1
    /// whenever a GLOBAL control owns the panel, and a per-deck chip built with deck -1
    /// names a shape that does not exist — the drawing carries (id, 0) and (id, 1), never
    /// (id, -1). Clicking such a chip took <see cref="FaceplateBoard.OnSelectionChanged"/>'s
    /// missing-shape path: the panel swapped to the new control while the board dimmed
    /// and lit nothing at all. Reproduced by selecting FX ON/OFF (global) and clicking
    /// the HI chip in its prose.</para>
    /// <para>Deck 0 is the same fallback the board's own ResolveDeck already uses for the
    /// partner highlight, so hovering a row and clicking a chip inside it now agree —
    /// before this the hover lit deck 1's knob and the click lit nothing.</para></summary>
    private int ChipDeck => _vm.SelectedDeck >= 0 ? _vm.SelectedDeck : 0;

    /// <summary>A chip's pointer entered it. Lights exactly the control it names —
    /// precise, unlike a hovered row, which highlights every participant in a
    /// combination at once.</summary>
    private void ChipHoverOn(string controlId, int deck) => _board.SetHover(controlId, deck, true);

    private void ChipHoverOff(string controlId, int deck) => _board.SetHover(controlId, deck, false);

    /// <summary>A chip was clicked. Goes through the exact same path a drawing shape's
    /// click uses, so an in-flight blink is cancelled the same way and only one
    /// control is ever held lit — see <see cref="FaceplateBoard.OnSelectionChanged"/>.
    /// </summary>
    private void ChipActivate(string controlId, int deck) => _vm.Select(controlId, deck);

    private string LayerNameOf(string layerId) =>
        _vm.Doc.Layers.FirstOrDefault(l => l.Id == layerId)?.Name ?? layerId;

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
        if (!isPlain) _layerBadgeText.Text = LayerNameOf(layerId).ToUpperInvariant();
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

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
        BuildScrim();
        RebuildHeadline();
        RebuildRows();
    }

    /// <summary>Repaints the backdrop scrim and the feather from the current theme.
    /// <para>The scrim used to be a flat <c>#C4000000</c>, which is 77% pure black and
    /// crushed every theme to the same near-zero luminance: Front Line Assembly's warm
    /// brown ground and Birthday Massacre's purple both arrived as black, and the guide
    /// was theme-blind by construction. It is now the theme's own <c>bgDeep</c>
    /// darkened, so the field keeps the app's hue while still going darker than any
    /// surface in it — the app behind stays legible as context and stops competing with
    /// the drawing for attention.</para>
    /// <para>The edge where the panel meets the board is three layers. The feather
    /// darkens the board on its approach; the panel's cast shadow gives it depth; and
    /// the panel's own leading edge is dipped into that shade before it comes up to
    /// full surface colour. The third one is the one that does the work: every Sholto
    /// theme is dark, so the field beside the panel is already close to black and a
    /// darker feather has almost nowhere left to go — but the panel is the LIGHTER of
    /// the two, so the step is a step UP, and the only place to soften it is on the
    /// panel's own side. The 1px border still terminates it as a crisp line.</para>
    /// </summary>
    private void BuildScrim()
    {
        var deep = this.ResourceColor("SholtoBgDeep", Color.FromRgb(0x11, 0x11, 0x11));
        Color Shade(byte alpha, double factor) => Color.FromArgb(
            alpha, (byte)(deep.R * factor), (byte)(deep.G * factor), (byte)(deep.B * factor));

        _backdrop.Background = new SolidColorBrush(Shade(0xF6, 0.55));

        var feather = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        feather.GradientStops.Add(new GradientStop(Shade(0x00, 0.0), 0.0));
        feather.GradientStops.Add(new GradientStop(Shade(0x70, 0.0), 0.55));
        feather.GradientStops.Add(new GradientStop(Shade(0xC8, 0.0), 1.0));
        _feather.Background = feather;

        var raised = this.ResourceColor("SholtoSurfaceRaised", Color.FromRgb(0x22, 0x22, 0x22));
        Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        var face = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        face.GradientStops.Add(new GradientStop(Mix(raised, deep, 0.82), 0.0));
        face.GradientStops.Add(new GradientStop(raised, 0.10));
        face.GradientStops.Add(new GradientStop(raised, 1.0));
        _panel.Background = face;
    }

}
