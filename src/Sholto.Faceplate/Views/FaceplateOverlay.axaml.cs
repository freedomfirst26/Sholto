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
/// <see cref="ControlSurface.IdProperty"/> this builds four parallel shapes on an
/// overlay canvas: a wide halo, a tight bloom, a fill tint and a transparent hit
/// target. Glows are blurred <i>rings</i> rather than filled shapes so the light
/// spills outward and the control underneath stays readable.</para></summary>
public partial class FaceplateOverlay : UserControl
{
    /// <summary>How far the bloom reaches beyond a control's edge, as a fraction of the
    /// control's short side. A fixed width swamped a 20px button and smeared into brown
    /// haze around the 440px platter, so the rings are cut to the control they belong to
    /// and clamped at both ends.</summary>
    private const double BloomFraction = 0.16;
    private const double BloomMin = 4, BloomMax = 10;
    /// <summary>The wider, softer ring, as a multiple of the bloom. Selection only — it
    /// is what makes the held state read as stronger than hover from across the board.
    /// </summary>
    private const double HaloMultiple = 1.6;

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

    private readonly FaceplateViewModel _vm;
    private readonly Dictionary<(string Id, int Deck), Piece> _pieces = new();
    private bool _built;
    private (string Id, int Deck)? _lit;
    private CancellationTokenSource? _blink;
    // Controls currently lit for a hover or a combination — as opposed to _lit, the
    // held selection, which always wins and is never touched by this set.
    private readonly List<(string Id, int Deck)> _highlighted = new();
    private readonly List<Control> _highlightLabels = new();

    public FaceplateOverlay() : this(FaceplateDocLoader.LoadEmbedded("ddj-flx4")) { }

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
        _vm = new FaceplateViewModel(doc);
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
        _vm.SelectionChanged += OnSelectionChanged;
        // A live gesture off the controller selects a control the same way a click does
        // (FaceplateViewModel.OnLiveGesture already updated Selected/SelectedDeck before
        // raising this), but announces it separately so it can guard against re-blinking
        // on every tick of a knob that's already selected. Route it through the exact
        // same blink-then-glow OnSelectionChanged uses for a click — not a second
        // animation — rather than duplicating BlinkThenSelect here.
        _vm.BlinkRequested += id => OnSelectionChanged(id, _vm.SelectedDeck);
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
                    ApplyHighlighted(_vm.Highlighted);
                    break;
            }
        };
        // Bounds are only real after a layout pass, and the overlay rectangles are
        // measured off the drawing rather than assumed, so the whole device layout can
        // be re-drawn without touching this file.
        LayoutUpdated += OnLayoutUpdated;
        ApplyTitle();
        _panelSummary.Text = _vm.Summary;
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
        if (_built || _stage.Bounds.Width <= 0) return;
        WireShapes();
        _built = true;
        LayoutUpdated -= OnLayoutUpdated;
    }

    /// <summary>Walk the drawing, find every shape that names a control, and build its
    /// overlay. This is the generic code that lets a device layout need no C#.</summary>
    private void WireShapes()
    {
        var found = new List<(Control Shape, string Id, int Deck, Rect Rect)>();
        var proxies = new List<(Control Shape, string Id, int? Deck, Rect Rect)>();
        foreach (var shape in _layoutHost.GetVisualDescendants().OfType<Control>())
        {
            var id = ControlSurface.GetId(shape);
            var hitFor = ControlSurface.GetHitFor(shape);
            if (id is null && hitFor is null) continue;
            var origin = shape.TranslatePoint(default, _stage);
            if (origin is null || shape.Bounds.Width <= 0) continue;
            var rect = new Rect(origin.Value, shape.Bounds.Size);
            if (id is not null)
                found.Add((shape, id, ControlSurface.GetDeck(shape), rect));
            else
                // Deck unset means "resolve it the way a prose chip does", and -1 is a
                // legitimate value (a global control), so absence has to be asked for
                // explicitly rather than inferred from the property's default.
                proxies.Add((shape, hitFor!,
                             shape.IsSet(ControlSurface.DeckProperty)
                                 ? ControlSurface.GetDeck(shape)
                                 : null,
                             rect));
        }

        // Glows first, hit targets afterwards, so a neighbour's bloom can never swallow
        // a click: z-order in a Canvas is declaration order.
        foreach (var (shape, id, deck, rect) in found)
        {
            var radius = CornerRadiusOf(shape);
            var implemented = _vm.IsImplemented(id);

            var t = Math.Clamp(Math.Min(rect.Width, rect.Height) * BloomFraction, BloomMin, BloomMax);
            var halo = Ring(rect, t * HaloMultiple, t, radius, blur: t * HaloMultiple);
            var bloom = Ring(rect, t, 0, radius, blur: t * 0.8);
            var tint = Solid(rect, radius, animated: true);
            var outline = Outline(rect, radius);
            // The eye integrates a wash over its area, so the same alpha that reads as a
            // hint on a 30px button reads as paint across a 440px platter and flattens
            // the drawing under it. Big faces get proportionally less.
            var wash = WashScale(rect);
            tint.Opacity = wash;
            tint.Fill = implemented ? FaceplateBrushes.AmberRest : null;

            _overlayCanvas.Children.Add(halo);
            _overlayCanvas.Children.Add(bloom);
            _overlayCanvas.Children.Add(tint);
            _overlayCanvas.Children.Add(outline);

            // What the wash gives up on a big face, the rim takes back: the jog would
            // otherwise be the least-marked control on the board despite being the most
            // used one.
            var piece = new Piece(halo, bloom, tint, outline, implemented,
                                  RestBloom: 0.20 + (1.0 - wash) * 0.40);
            // Put it in its resting state explicitly rather than relying on the shapes'
            // construction values. They are not the same thing, and when they drifted
            // apart only the controls a pointer had swept over ever looked "at rest".
            ApplyRest(piece);
            _pieces[(id, deck)] = piece;
        }

        // Legend proxies go down BEFORE the controls' own hit targets, so that wherever
        // a legend box grazes a neighbouring control the control wins. Two do: the
        // "ACTIVE" and "DEL" boxes on each deck clip the top few pixels of the 440px
        // jog disc. A legend must never steal a click from a control.
        foreach (var (shape, id, deck, rect) in proxies)
        {
            // A legend naming an id the drawing has no shape for can light nothing and
            // would select a control the board cannot show. Leave it inert rather than
            // teach the reader something false — this is the guard against a typo in a
            // HitFor, which is otherwise silent.
            if (!_pieces.Keys.Any(k => k.Id == id)) continue;

            var hit = BuildHit(rect, CornerRadiusOf(shape), id);
            // The deck is resolved at the moment of the gesture, not here: an unset deck
            // means "whichever deck is live", and that changes while the guide is open.
            // Latched on enter so the exit un-hovers exactly what the enter lit, even if
            // the selection moved decks in between.
            (string Id, int Deck)? hovered = null;
            hit.PointerEntered += (_, _) =>
            {
                hovered = (id, deck ?? ResolveDeck(id));
                ApplyHover(hovered.Value, true);
            };
            hit.PointerExited += (_, _) =>
            {
                if (hovered is { } k) ApplyHover(k, false);
                hovered = null;
            };
            hit.PointerPressed += (_, e) =>
            {
                e.Handled = true;   // do not fall through to the backdrop's close
                _vm.Select(id, deck ?? ResolveDeck(id));
            };
            // Carried onto the generated shape purely so the built overlay says out loud
            // which control each proxy speaks for.
            ControlSurface.SetHitFor(hit, id);
            if (deck is { } d) ControlSurface.SetDeck(hit, d);
            _overlayCanvas.Children.Add(hit);
        }

        foreach (var (shape, id, deck, rect) in found)
        {
            var hit = BuildHit(rect, CornerRadiusOf(shape), id);
            var key = (id, deck);
            hit.PointerEntered += (_, _) => ApplyHover(key, true);
            hit.PointerExited += (_, _) => ApplyHover(key, false);
            hit.PointerPressed += (_, e) =>
            {
                e.Handled = true;   // do not fall through to the backdrop's close
                _vm.Select(id, deck);
            };
            _overlayCanvas.Children.Add(hit);
        }
    }

    /// <summary>The transparent shape that answers the pointer for a control. Shared by
    /// a control's own body and by a legend that speaks for it, so the two are the same
    /// target in every respect but where they sit.</summary>
    private Shape BuildHit(Rect rect, double radius, string id)
    {
        var hit = Solid(rect, radius, animated: false);
        hit.Fill = Brushes.Transparent;
        hit.IsHitTestVisible = true;
        // A hand over a control Sholto never hears from contradicts the board's own
        // language: the paint says "nothing here", the pointer says "button". They
        // stay clickable — the panel explains why they do nothing, which is worth
        // reading — but the cursor no longer promises an action.
        hit.Cursor = new Cursor(_vm.IsImplemented(id)
            ? StandardCursorType.Hand
            : StandardCursorType.Arrow);
        return hit;
    }

    // ---- the four states -------------------------------------------------------

    /// <summary>While something is selected, every OTHER implemented control drops to
    /// this bloom. The board recedes so the one lit thing is the only thing lit; without
    /// it, selection competed with forty resting glows.</summary>
    private const double DimmedRestBloom = 0.10;

    /// <summary>Rest: a control Sholto acts on is tinted. One it does not is left with no
    /// colour at all — that absence IS the signal.</summary>
    private void ApplyRest(Piece p)
    {
        p.Tint.Fill = p.Implemented ? FaceplateBrushes.AmberRest : null;
        // A live control also carries a faint bloom at rest. The wash alone reads by
        // area, so a 440px platter would shout where a 20px button whispered; the ring
        // hugs the edge and so says the same thing at every size. Held down to
        // DimmedRestBloom while some other control owns the panel.
        p.Bloom.Opacity = p.Implemented
            ? (_lit is null ? p.RestBloom : DimmedRestBloom)
            : 0;
        p.Halo.Opacity = 0;
        p.Outline.Opacity = 0;
    }

    /// <summary>Re-seats every control that is neither selected nor currently lit for a
    /// hover or a partner highlight. Called whenever the selection appears or goes away,
    /// because that is what changes which resting bloom the rest of the board carries.
    /// </summary>
    private void ApplyRestToOthers()
    {
        foreach (var (key, piece) in _pieces)
        {
            if (key == _lit || _highlighted.Contains(key)) continue;
            ApplyRest(piece);
        }
    }

    /// <summary>Hover: the same amber, blooming outward. Not an outline.</summary>
    private void ApplyHover((string Id, int Deck) key, bool on)
    {
        if (_lit == key) return;                    // selection outranks hover
        if (!_pieces.TryGetValue(key, out var p)) return;
        if (!on) { ApplyRest(p); return; }
        if (!p.Implemented)
        {
            // A control Sholto never hears from still answers the pointer — you can click
            // it and read why it does nothing — but it must not bloom like a live one, or
            // hovering would contradict the whole colour language.
            p.Bloom.Opacity = 0.3;
            return;
        }
        p.Outline.Opacity = 0;
        p.Tint.Fill = FaceplateBrushes.AmberHoverFill;
        p.Bloom.Opacity = 0.85;
        p.Halo.Opacity = 0.28;
    }

    /// <summary>Selected: brighter and wider than hover, held until something else is
    /// picked, so a reader never loses track of what the panel is describing.
    /// <para>A control Sholto never hears from (TRIM, MASTER LEVEL, …) still needs to
    /// say "this is the one the panel is about" when clicked — but not by lighting up
    /// amber, which would contradict its own summary text saying it never lights up
    /// here. It gets a plain ring instead, with no fill and no amber bloom.</para></summary>
    private static void ApplySelected(Piece p)
    {
        // The stroke is what says "this one", and it says it identically at every size
        // and on both kinds of control. What still separates them is everything else:
        // a live control keeps its wash and its bloom, a dead one has neither, so the
        // colour rule is untouched — the outline marks the panel's subject, the fill
        // marks whether Sholto hears from it.
        p.Outline.Opacity = 1.0;
        if (!p.Implemented)
        {
            p.Tint.Fill = null;
            p.Bloom.Opacity = 0;
            p.Halo.Opacity = 0.5;
            return;
        }
        p.Tint.Fill = FaceplateBrushes.AmberSelectedFill;
        p.Bloom.Opacity = 1.0;
        p.Halo.Opacity = 0.95;
    }

    private void OnSelectionChanged(string? id, int deck)
    {
        _blink?.Cancel();
        _blink?.Dispose();
        _blink = null;
        if (_lit is { } prev && _pieces.TryGetValue(prev, out var old)) ApplyRest(old);
        _lit = null;

        if (id is null)
        {
            // Nothing selected any more: the board comes back up to full resting glow.
            ApplyRestToOthers();
            return;
        }
        var key = (id, deck);
        if (!_pieces.TryGetValue(key, out var p))
        {
            // No shape for this (id, deck) — every control currently in the layout
            // has a matching shape, so this can't fire today, but a future device
            // layout could omit one. Without this, _lit is already null (above) but
            // the board never gets told to come back up, so it would stay pinned at
            // the dimmed resting bloom. Recover the same way as the "nothing
            // selected" branch above.
            ApplyRestToOthers();
            return;
        }
        _lit = key;
        ApplyRestToOthers();

        // Title and summary come through the view model's own properties now (they
        // know how to phrase a layer view as well as a plain selection); this only
        // owns the drawing's blink-then-glow.
        _blink = new CancellationTokenSource();
        _ = BlinkThenSelect(p, _blink.Token);
    }

    /// <summary>Two soft pulses, then settle into the sustained glow. Called both when
    /// the shape is clicked on screen and when the matching control is pressed on the
    /// physical unit, so the two feel like the same action.
    /// <para>The pulses ride the shapes' own opacity transitions — it breathes rather
    /// than strobes, so it acknowledges the click without reading as a warning.</para>
    /// </summary>
    private async Task BlinkThenSelect(Piece p, CancellationToken ct)
    {
        try
        {
            for (var pulse = 0; pulse < 2; pulse++)
            {
                ApplySelected(p);
                await Task.Delay(190, ct);
                // A delay that has ALREADY elapsed cannot be cancelled: its continuation
                // is queued and resumes whatever the token says. Without a check here it
                // would go on writing to a piece that OnSelectionChanged has already put
                // back to rest — leaving two controls held lit, or one glowing with
                // nothing selected and the panel shut, with no way back because _lit no
                // longer points at it.
                ct.ThrowIfCancellationRequested();
                // Mirrors ApplyRest's implemented/unimplemented split — the dip between
                // pulses must not show the amber resting tint on a control that never
                // carries one at rest.
                p.Tint.Fill = p.Implemented ? FaceplateBrushes.AmberRest : null;
                p.Bloom.Opacity = p.Implemented ? p.RestBloom : 0;
                p.Halo.Opacity = 0.08;
                await Task.Delay(190, ct);
                ct.ThrowIfCancellationRequested();
            }
            // ApplySelected is reachable from here and from the loop above, and nowhere
            // else — both are now behind a cancellation check, so no settle can outlive
            // the selection that asked for it.
            ApplySelected(p);
        }
        catch (OperationCanceledException) { /* another control was picked */ }
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
            ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 22);
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
        _headlinePanel.Children.Add(head);
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
        Background = ResourceBrush("SholtoBorder", Brushes.DimGray),
        IsHitTestVisible = false,
    };

    /// <summary>Rebuilds the bullet list from <see cref="FaceplateViewModel.Rows"/>:
    /// everything the selected control is involved in besides its headline — its own
    /// remaining gestures, and any gesture owned by another control that names this one
    /// as a partner.</summary>
    private void RebuildRows()
    {
        _rowsPanel.Children.Clear();
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
        var nameForeground = ResourceBrush("SholtoTextBright", Brushes.White);
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
                Background = ResourceBrush("SholtoAccentBg", Brushes.Transparent),
                Child = new TextBlock
                {
                    Text = LayerNameOf(row.Layer).ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    LetterSpacing = 0.8,
                    LineHeight = 13,
                    Foreground = ResourceBrush("SholtoAccent", Brushes.Orange),
                },
            });
        }
        stack.Children.Add(nameControl);

        // Same 13px as the gesture name above it. Weight and opacity do the separating,
        // which keeps the whole panel on four sizes: 18 title, 16 answer, 13 body,
        // 11 eyebrow and cap.
        var result = BuildProseText(row.Result, 13, FontWeight.Normal,
            ResourceBrush("SholtoTextBright", Brushes.White), lineHeight: 18);
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
            result.Foreground = ResourceBrush("SholtoTextMuted", Brushes.Gray);
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
            border.Background = ResourceBrush("SholtoAccentBg", Brushes.Transparent);
            _vm.HoverRow(row.GestureId);
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            _vm.HoverRow(null);
        };
        return border;
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
    /// (id, -1). Clicking such a chip took <see cref="OnSelectionChanged"/>'s
    /// missing-shape path: the panel swapped to the new control while the board dimmed
    /// and lit nothing at all. Reproduced by selecting FX ON/OFF (global) and clicking
    /// the HI chip in its prose.</para>
    /// <para>Deck 0 is the same fallback <see cref="ResolveDeck"/> already uses for the
    /// partner highlight, so hovering a row and clicking a chip inside it now agree —
    /// before this the hover lit deck 1's knob and the click lit nothing.</para></summary>
    private int ChipDeck => _vm.SelectedDeck >= 0 ? _vm.SelectedDeck : 0;

    /// <summary>A chip's pointer entered it. Lights exactly the control it names —
    /// precise, unlike a hovered row, which highlights every participant in a
    /// combination at once.</summary>
    private void ChipHoverOn(string controlId, int deck) => ApplyHover((controlId, deck), true);

    private void ChipHoverOff(string controlId, int deck) => ApplyHover((controlId, deck), false);

    /// <summary>A chip was clicked. Goes through the exact same path a drawing shape's
    /// click uses, so an in-flight blink is cancelled the same way and only one
    /// control is ever held lit — see <see cref="OnSelectionChanged"/>.</summary>
    private void ChipActivate(string controlId, int deck) => _vm.Select(controlId, deck);

    private string LayerNameOf(string layerId) =>
        _vm.Doc.Layers.FirstOrDefault(l => l.Id == layerId)?.Name ?? layerId;

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Resolves one of the host's <c>Sholto…</c> theme brushes.
    /// <para>The second lookup is not belt-and-braces, it is the one that works. A
    /// resource lookup started at this control returns nothing for these keys even
    /// once the overlay is attached and the very same keys resolve from the window —
    /// verified by printing both at runtime, attached, with the app's theme live:
    /// <c>this.TryGetResource</c> false, <c>TopLevel.TryGetResource</c> true. Sholto
    /// declares its palette in <c>Window.Resources</c> and rewrites it there on every
    /// theme change, so the window is where these keys live; ask it directly. Without
    /// this every colour in the panel silently fell back to the hard-coded literal
    /// passed in — grey labels and an ORANGE eyebrow in an eleven-theme app.</para>
    /// </summary>
    private IBrush ResourceBrush(string key, IBrush fallback)
    {
        if (this.TryGetResource(key, ActualThemeVariant, out var v) && v is IBrush b) return b;
        var top = TopLevel.GetTopLevel(this);
        if (top is not null && top.TryGetResource(key, top.ActualThemeVariant, out var w) && w is IBrush c)
            return c;
        return fallback;
    }

    /// <summary>The theme colours are exposed as <c>IBrush</c>, not <c>Color</c>, so a
    /// <c>GradientStop Color="{DynamicResource …}"</c> cannot resolve one. Anything
    /// theme-derived that needs a raw colour — the scrim, the feather — unwraps it
    /// here instead of being written in XAML.</summary>
    private Color ResourceColor(string key, Color fallback) =>
        ResourceBrush(key, new SolidColorBrush(fallback)) is SolidColorBrush b ? b.Color : fallback;

    private IBrush? _themeStamp;

    /// <summary>Repaints everything in the panel that took a colour by value rather
    /// than by binding — the scrim, the feather, the panel face, and the headline and
    /// bullet text, which are built in code and hold brush references rather than
    /// DynamicResource bindings. <c>ResourcesChanged</c> fires dozens of times during
    /// startup, so the palette's own brush instance is the guard: the theme changed
    /// only if the app swapped that object.</summary>
    private void ApplyTheme()
    {
        var stamp = ResourceBrush("SholtoSurfaceRaised", Brushes.Transparent);
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
        var deep = ResourceColor("SholtoBgDeep", Color.FromRgb(0x11, 0x11, 0x11));
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

        var raised = ResourceColor("SholtoSurfaceRaised", Color.FromRgb(0x22, 0x22, 0x22));
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

    // ---- partner highlight -------------------------------------------------------

    /// <summary>Lights every control a hovered row or a selected combination names, and
    /// numbers the ones that are steps in a combination. Reuses the hover visual state
    /// rather than inventing a fifth one — a highlighted control looks exactly like one
    /// under the pointer.</summary>
    private void ApplyHighlighted(IReadOnlyList<(string ControlId, int Step)> highlighted)
    {
        if (!_built) return;

        var next = highlighted
            .Select(h => (Key: (h.ControlId, ResolveDeck(h.ControlId)), h.Step))
            .Where(h => _pieces.ContainsKey(h.Key))
            .ToList();
        var nextKeys = next.Select(h => h.Key).ToHashSet();

        foreach (var key in _highlighted)
            if (!nextKeys.Contains(key)) ApplyHover(key, false);

        foreach (var label in _highlightLabels) _overlayCanvas.Children.Remove(label);
        _highlightLabels.Clear();
        _highlighted.Clear();

        foreach (var (key, step) in next)
        {
            ApplyHover(key, true);
            _highlighted.Add(key);
            if (step > 0) _highlightLabels.Add(AddStepLabel(key, step));
        }
    }

    /// <summary>A per-deck partner is resolved on whatever deck is currently selected
    /// (they are the same physical deck); a global one has no deck at all.</summary>
    private int ResolveDeck(string controlId)
    {
        var control = _vm.Doc.Controls.FirstOrDefault(c => c.Id == controlId);
        if (control is null || control.Scope != "per-deck") return -1;
        return _vm.SelectedDeck >= 0 ? _vm.SelectedDeck : 0;
    }

    private Control AddStepLabel((string Id, int Deck) key, int step)
    {
        var tint = _pieces[key].Tint;
        var left = Canvas.GetLeft(tint);
        var top = Canvas.GetTop(tint);
        // Top-CENTRE of the shape's bounding box, not its corner: the jog is a 440px
        // circle, and a badge planted at the box corner lands off in empty space,
        // nowhere near the ring it is meant to number. Top-centre sits just above the
        // shape for every size, round or square.
        var centerX = left + tint.Width / 2;
        // U+2460 ① … circled digits read as ordered steps without competing with the
        // amber glow underneath.
        var glyph = step is >= 1 and <= 20 ? char.ConvertFromUtf32(0x2460 + step - 1) : step.ToString();
        var label = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = FaceplateBrushes.AmberGlowBrush,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = glyph,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Canvas.SetLeft(label, centerX - 11);
        Canvas.SetTop(label, top - 11);
        _overlayCanvas.Children.Add(label);
        return label;
    }

    // ---- overlay geometry ------------------------------------------------------

    /// <summary>A blurred ring hugging the control. <paramref name="thickness"/> is how
    /// far it reaches out; <paramref name="inset"/> pushes it further out so two rings
    /// can sit concentrically without overlapping.</summary>
    private static Shape Ring(Rect r, double thickness, double inset, double radius, double blur)
    {
        var outer = r.Inflate(thickness + inset);
        var s = Build(outer, radius < 0 ? radius : radius + thickness + inset);
        s.Fill = null;
        s.Stroke = FaceplateBrushes.AmberGlowBrush;
        s.StrokeThickness = thickness;
        s.Effect = new BlurEffect { Radius = blur };
        s.Opacity = 0;
        Fade(s);
        Place(s, outer);
        return s;
    }

    /// <summary>The selection stroke: a 2px solid ring hugging the control, amber, no
    /// blur. Every other mark on this board is soft — washes, blurred rings, blooms —
    /// so a hard edge is a change of KIND rather than of degree, and that is the whole
    /// point of it. Selection used to be "the same glow, a bit stronger", which a
    /// 440px platter could carry and a 30px pad could not: against a field where every
    /// implemented control already blooms, one more increment of bloom is invisible.
    /// </summary>
    private static Shape Outline(Rect r, double radius)
    {
        var outer = r.Inflate(3);
        var s = Build(outer, radius < 0 ? radius : radius + 3);
        s.Fill = null;
        s.Stroke = FaceplateBrushes.AmberGlowBrush;
        s.StrokeThickness = 2;
        s.Opacity = 0;
        Fade(s);
        Place(s, outer);
        return s;
    }

    /// <summary>How much of the resting wash a control of this size should carry: full
    /// for anything button-sized, tapering to just over half for the jog platter.</summary>
    private static double WashScale(Rect r)
    {
        var side = Math.Min(r.Width, r.Height);
        if (side <= 70) return 1.0;
        return Math.Max(0.70, 1.0 - (side - 70) / 1200.0);
    }

    /// <summary>A shape filling the control exactly. <paramref name="animated"/> is false
    /// for the hit target, whose opacity never changes.</summary>
    private static Shape Solid(Rect r, double radius, bool animated)
    {
        var s = Build(r, radius);
        if (animated) Fade(s);
        Place(s, r);
        return s;
    }

    /// <summary>A negative radius means "circle" — see <see cref="CornerRadiusOf"/>. A
    /// square-cornered Border reports 0 and still wants a Rectangle, which is why the
    /// caller passes the radius rather than this guessing from the bounds.</summary>
    private static Shape Build(Rect r, double radius) => radius < 0
        ? new Ellipse { Width = r.Width, Height = r.Height }
        : new Rectangle { Width = r.Width, Height = r.Height, RadiusX = radius, RadiusY = radius };

    private static void Place(Control c, Rect r)
    {
        Canvas.SetLeft(c, r.X);
        Canvas.SetTop(c, r.Y);
        c.IsHitTestVisible = false;
    }

    private static void Fade(Avalonia.Animation.Animatable c) => c.Transitions =
    [
        new Avalonia.Animation.DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(170),
            Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
        },
    ];

    private static double CornerRadiusOf(Control c) => c switch
    {
        Ellipse => -1,                            // circle: no radius, use an Ellipse
        Border b => b.CornerRadius.TopLeft,
        _ => 0,
    };

    private sealed record Piece(Shape Halo, Shape Bloom, Shape Tint, Shape Outline,
                                bool Implemented, double RestBloom);
}
