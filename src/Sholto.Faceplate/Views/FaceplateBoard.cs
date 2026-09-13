using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Sholto.Faceplate.Devices.DdjFlx4;
using Sholto.Faceplate.Model;

namespace Sholto.Faceplate.Views;

/// <summary>The device drawing: paints the amber colour language over it, and answers
/// hover and click. Split out of <see cref="FaceplateOverlay"/>, which still owns the
/// gesture panel and the theme-driven chrome around this board.
/// <para><b>Amber marks what Sholto uses.</b> Four states, one colour, rising
/// intensity: a control the app acts on carries a resting amber tint; nothing else
/// carries any colour; hover lifts the tint and blooms outward; a click pulses twice
/// and settles into a held glow that is unmistakably the strongest of the three. There
/// is no legend, deliberately — a coloured control is live, a plain one is not.</para>
/// <para>The drawing is never touched. For every shape carrying a
/// <see cref="DeviceLayout.IdProperty"/> this builds four parallel shapes on an
/// overlay canvas: a wide halo, a tight bloom, a fill tint and a transparent hit
/// target. Glows are blurred <i>rings</i> rather than filled shapes so the light
/// spills outward and the control underneath stays readable.</para>
/// <para>Takes the drawing's own hosts plus narrow callbacks for its only view-model
/// contact — whether a control is implemented, which deck the panel is showing, and
/// selecting a control — rather than a back-reference to the overlay or its view
/// model.</para></summary>
public sealed class FaceplateBoard
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

    private readonly Grid _stage;
    private readonly ContentControl _layoutHost;
    private readonly Canvas _overlayCanvas;
    private readonly Func<string, bool> _isImplemented;
    private readonly Func<int> _selectedDeck;
    private readonly Action<string, int> _select;
    private readonly Func<string, bool> _isPerDeckControl;

    private readonly Dictionary<(string Id, int Deck), Piece> _pieces = new();
    private bool _built;
    private (string Id, int Deck)? _lit;
    private CancellationTokenSource? _blink;
    // Controls currently lit for a hover or a combination — as opposed to _lit, the
    // held selection, which always wins and is never touched by this set.
    private readonly List<(string Id, int Deck)> _highlighted = new();
    private readonly List<Control> _highlightLabels = new();

    /// <summary>Whether <see cref="WireShapes"/> has run. Bounds are only real after a
    /// layout pass, so the host defers wiring until then; this lets it — and
    /// <see cref="ApplyHighlighted"/>, which can otherwise be asked to light a board
    /// that has no shapes yet — both check the same state.</summary>
    public bool IsBuilt => _built;

    public FaceplateBoard(Grid stage, ContentControl layoutHost, Canvas overlayCanvas,
                          Func<string, bool> isImplemented, Func<int> selectedDeck,
                          Action<string, int> select, Func<string, bool> isPerDeckControl)
    {
        _stage = stage;
        _layoutHost = layoutHost;
        _overlayCanvas = overlayCanvas;
        _isImplemented = isImplemented;
        _selectedDeck = selectedDeck;
        _select = select;
        _isPerDeckControl = isPerDeckControl;
    }

    /// <summary>Walk the drawing, find every shape that names a control, and build its
    /// overlay. This is the generic code that lets a device layout need no C#.</summary>
    public void WireShapes()
    {
        var found = new List<(Control Shape, string Id, int Deck, Rect Rect)>();
        var proxies = new List<(Control Shape, string Id, int? Deck, Rect Rect)>();
        foreach (var shape in _layoutHost.GetVisualDescendants().OfType<Control>())
        {
            var id = DeviceLayout.GetId(shape);
            var hitFor = DeviceLayout.GetHitFor(shape);
            if (id is null && hitFor is null) continue;
            var origin = shape.TranslatePoint(default, _stage);
            if (origin is null || shape.Bounds.Width <= 0) continue;
            var rect = new Rect(origin.Value, shape.Bounds.Size);
            if (id is not null)
                found.Add((shape, id, DeviceLayout.GetDeck(shape), rect));
            else
                // Deck unset means "resolve it the way a prose chip does", and -1 is a
                // legitimate value (a global control), so absence has to be asked for
                // explicitly rather than inferred from the property's default.
                proxies.Add((shape, hitFor!,
                             shape.IsSet(DeviceLayout.DeckProperty)
                                 ? DeviceLayout.GetDeck(shape)
                                 : null,
                             rect));
        }

        // Glows first, hit targets afterwards, so a neighbour's bloom can never swallow
        // a click: z-order in a Canvas is declaration order.
        foreach (var (shape, id, deck, rect) in found)
        {
            var radius = CornerRadiusOf(shape);
            var implemented = _isImplemented(id);

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
                _select(id, deck ?? ResolveDeck(id));
            };
            // Carried onto the generated shape purely so the built overlay says out loud
            // which control each proxy speaks for.
            DeviceLayout.SetHitFor(hit, id);
            if (deck is { } d) DeviceLayout.SetDeck(hit, d);
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
                _select(id, deck);
            };
            _overlayCanvas.Children.Add(hit);
        }

        _built = true;
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
        hit.Cursor = new Cursor(_isImplemented(id)
            ? StandardCursorType.Hand
            : StandardCursorType.Arrow);
        return hit;
    }

    /// <summary>A chip in the gesture panel's prose named this control — lights or
    /// un-lights it exactly as if the pointer were over its own shape on the drawing.
    /// </summary>
    public void SetHover(string controlId, int deck, bool on) => ApplyHover((controlId, deck), on);

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

    public void OnSelectionChanged(string? id, int deck)
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

    // ---- partner highlight -------------------------------------------------------

    /// <summary>Lights every control a hovered row or a selected combination names, and
    /// numbers the ones that are steps in a combination. Reuses the hover visual state
    /// rather than inventing a fifth one — a highlighted control looks exactly like one
    /// under the pointer.</summary>
    public void ApplyHighlighted(IReadOnlyList<(string ControlId, int Step)> highlighted)
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
        if (!_isPerDeckControl(controlId)) return -1;
        var selectedDeck = _selectedDeck();
        return selectedDeck >= 0 ? selectedDeck : 0;
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
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
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
            Property = Visual.OpacityProperty,
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
