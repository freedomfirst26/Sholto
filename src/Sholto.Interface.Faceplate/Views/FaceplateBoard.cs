using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;
using Sholto.Interface.Faceplate.Model;

namespace Sholto.Interface.Faceplate.Views;

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
public sealed class FaceplateBoard(Grid stage, ContentControl layoutHost, Canvas overlayCanvas,
                                   Func<string, bool> isImplemented, Func<int> selectedDeck,
                                   Action<string, int> select, Func<string, bool> isPerDeckControl,
                                   FaceplateBrushes brushes, IFaceplateShapeFactory shapes,
                                   IFaceplateStepBadgeFactory badges)
{
    private readonly Grid _stage = stage;
    private readonly ContentControl _layoutHost = layoutHost;
    private readonly Canvas _overlayCanvas = overlayCanvas;
    private readonly Func<string, bool> _isImplemented = isImplemented;
    private readonly Func<int> _selectedDeck = selectedDeck;
    private readonly Action<string, int> _select = select;
    private readonly Func<string, bool> _isPerDeckControl = isPerDeckControl;
    private readonly FaceplateBrushes _brushes = brushes;
    private readonly IFaceplateShapeFactory _shapes = shapes;
    private readonly IFaceplateStepBadgeFactory _badges = badges;

    private readonly Dictionary<(string Id, int Deck), FaceplatePiece> _pieces = new();
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
            var implemented = _isImplemented(id);
            var piece = _shapes.CreatePiece(shape, rect, implemented);
            _overlayCanvas.Children.Add(piece.Halo);
            _overlayCanvas.Children.Add(piece.Bloom);
            _overlayCanvas.Children.Add(piece.Tint);
            _overlayCanvas.Children.Add(piece.Outline);
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

            var hit = _shapes.CreateHit(shape, rect, _isImplemented(id));
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
            var hit = _shapes.CreateHit(shape, rect, _isImplemented(id));
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
    private void ApplyRest(FaceplatePiece p)
    {
        p.Tint.Fill = p.Implemented ? _brushes.AmberRest : null;
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
        p.Tint.Fill = _brushes.AmberHoverFill;
        p.Bloom.Opacity = 0.85;
        p.Halo.Opacity = 0.28;
    }

    /// <summary>Selected: brighter and wider than hover, held until something else is
    /// picked, so a reader never loses track of what the panel is describing.
    /// <para>A control Sholto never hears from (TRIM, MASTER LEVEL, …) still needs to
    /// say "this is the one the panel is about" when clicked — but not by lighting up
    /// amber, which would contradict its own summary text saying it never lights up
    /// here. It gets a plain ring instead, with no fill and no amber bloom.</para></summary>
    private void ApplySelected(FaceplatePiece p)
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
        p.Tint.Fill = _brushes.AmberSelectedFill;
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
    private async Task BlinkThenSelect(FaceplatePiece p, CancellationToken ct)
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
                p.Tint.Fill = p.Implemented ? _brushes.AmberRest : null;
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
            if (step > 0)
            {
                var badge = _badges.Create(_pieces[key].Tint, step);
                _overlayCanvas.Children.Add(badge);
                _highlightLabels.Add(badge);
            }
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

}
