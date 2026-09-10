using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Controller;
using Sholto.Controller.Gestures;
using Sholto.Faceplate.Model;

namespace Sholto.Faceplate.ViewModels;

/// <summary>What the controller guide is currently showing. Holds the loaded document,
/// which control is selected and on which deck, and whether the side panel is open.
/// <para>The panel is per-control throughout: it shows what the selected control is
/// INVOLVED IN, not only what it owns. Clicking SHIFT lists the three shift chords even
/// though none of them belongs to SHIFT — each one belongs to the control it acts on,
/// and simply names SHIFT as a partner. Task 12 calls <see cref="Select"/> from live
/// gestures, so a control pressed on the physical unit lights the same way a clicked one
/// does.</para></summary>
public sealed class FaceplateViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, ControlDoc> _byId;
    private readonly HashSet<string> _implemented;
    // Every gesture across every control, each carrying the control it belongs to. This
    // is what makes the involvement lookup possible without a second copy of the data:
    // "what involves this control" is just a filter over the same list "what this
    // control owns" comes from.
    private readonly IReadOnlyList<(ControlDoc Control, GestureDoc Gesture)> _allGestures;

    public FaceplateViewModel(FaceplateDoc doc)
    {
        Doc = doc;
        _byId = doc.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        // Implemented means Sholto acts on at least one gesture of this control. A
        // control with no gestures, or only unused ones, gets no colour at all — the
        // absence of colour IS the signal, which is why the overlay has no legend.
        _implemented = doc.Controls
            .Where(c => c.Gestures.Any(g => g.Used))
            .Select(c => c.Id)
            .ToHashSet(StringComparer.Ordinal);
        _allGestures = doc.Controls
            .SelectMany(c => c.Gestures.Select(g => (Control: c, Gesture: g)))
            .ToList();
    }

    /// <summary>The loaded guide. Read-only to the view.</summary>
    public FaceplateDoc Doc { get; }

    /// <summary>Header text for the overlay. Reads e.g. "Jog wheel · deck 1" once a
    /// per-deck control is selected, else just the device name.</summary>
    public string Title => _selected is not null
        ? _selectedDeck >= 0
            ? $"{_selected.Label} · deck {_selectedDeck + 1}"
            : _selected.Label
        : Doc.Device;

    /// <summary>Text under the title: the selected control's own summary. Null when
    /// nothing is selected.</summary>
    public string? Summary => _selected?.Summary;

    private ControlDoc? _selected;
    public ControlDoc? Selected
    {
        get => _selected;
        private set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            Raise();
            Raise(nameof(IsPanelOpen));
            Raise(nameof(Title));
            Raise(nameof(Summary));
            Raise(nameof(Headline));
            Raise(nameof(Rows));
        }
    }

    private int _selectedDeck = -1;
    /// <summary>0 or 1 for a per-deck control, -1 for a global one. Two shapes share an
    /// id when the unit has one on each deck; this says which was picked.</summary>
    public int SelectedDeck
    {
        get => _selectedDeck;
        private set
        {
            if (_selectedDeck == value) return;
            _selectedDeck = value;
            Raise();
            Raise(nameof(Title));
        }
    }

    private string? _hoverGestureId;

    /// <summary>The one answer to "what does a plain press of this button do?" — the
    /// selected control's own plain-layer gesture, provided it names no partner and is
    /// the only one. A control with several plain gestures at once (the jog: turn,
    /// touch, ring) has no single such answer, so this is null and every gesture — the
    /// jog's included — shows up in <see cref="Rows"/> instead. Null while nothing is
    /// selected, or for a control with no gestures at all (TRIM): the summary alone
    /// explains those.</summary>
    public GestureRow? Headline
    {
        get
        {
            if (_selected is null) return null;
            var candidates = _selected.Gestures
                .Where(g => g.Layer == "plain" && g.With is not { Count: > 0 })
                .ToList();
            return candidates.Count == 1 ? ToRow(candidates[0], _selected.Id) : null;
        }
    }

    /// <summary>Everything else the selected control is involved in: its own gestures
    /// (bar the one already promoted to <see cref="Headline"/>), plus any gesture owned
    /// by ANOTHER control that names this one as a partner — so SHIFT lists the three
    /// chords it unlocks even though it owns only "hold". Empty for a control with
    /// nothing at all (TRIM); the summary explains why.</summary>
    public IReadOnlyList<GestureRow> Rows
    {
        get
        {
            if (_selected is null) return [];
            var headline = Headline;
            return InvolvementFor(_selected)
                .Where(e => headline is null
                            || e.Gesture.Id != headline.GestureId
                            || e.OwnerId != headline.OwnerControlId)
                .Select(e => ToRow(e.Gesture, e.OwnerId))
                .ToList();
        }
    }

    /// <summary>Which controls to light up right now, and what step number to draw
    /// beside each one, driven purely by hover: a row with a partner numbers every
    /// participant in order — the partner(s) first, the gesture's own control last — and
    /// a row with none highlights nothing.</summary>
    public IReadOnlyList<(string ControlId, int Step)> Highlighted
    {
        get
        {
            if (_hoverGestureId is null || _selected is null) return [];
            var entry = InvolvementFor(_selected).FirstOrDefault(e => e.Gesture.Id == _hoverGestureId);
            if (entry.Gesture is null || entry.Gesture.With is not { Count: > 0 }) return [];
            var steps = new List<(string, int)>();
            var step = 1;
            foreach (var partner in entry.Gesture.With) steps.Add((partner, step++));
            steps.Add((entry.OwnerId, step));
            return steps;
        }
    }

    /// <summary>The side panel is open exactly when something is selected. There is no
    /// separate setter: an empty panel would be a state with nothing to say.</summary>
    public bool IsPanelOpen => _selected is not null;

    /// <summary>True when Sholto acts on at least one gesture of this control.</summary>
    public bool IsImplemented(string controlId) => _implemented.Contains(controlId);

    /// <summary>Raised after a selection changes, with the id and deck now held. The view
    /// listens so a selection made from a live gesture lights the same shape a click
    /// would have lit.</summary>
    public event Action<string?, int>? SelectionChanged;

    /// <summary>Select a control by id and deck. An id with no entry in the guide is
    /// ignored rather than opening a blank panel. Drops any standing hover — a fresh
    /// selection always starts from its own, unfiltered involvement list.</summary>
    public void Select(string controlId, int deck)
    {
        if (!SelectCore(controlId, deck)) return;
        // A click names no gesture, so it never gets to claim a row of the panel it
        // just opened — only a live gesture earns that (see OnLiveGesture, which sets
        // ActiveRowId itself, before ever reaching SelectCore).
        ActiveRowId = null;
        SelectionChanged?.Invoke(controlId, deck);
    }

    /// <summary>The state change shared by a click (<see cref="Select"/>, which always
    /// re-announces via <see cref="SelectionChanged"/>, even for a re-click on the
    /// control already selected) and a live gesture (<see cref="OnLiveGesture"/>, which
    /// announces only when the owning control actually changed — see there for why).
    /// Returns false, doing nothing, for an id the guide doesn't know.</summary>
    private bool SelectCore(string controlId, int deck)
    {
        if (!_byId.TryGetValue(controlId, out var control)) return false;
        _hoverGestureId = null;
        SelectedDeck = deck;
        Selected = control;
        Raise(nameof(Highlighted));
        return true;
    }

    private string? _activeRowId;
    /// <summary>The gesture id behind the most recent live selection — which ROW of the
    /// selected control's involvement list to highlight, not just which control. Holding
    /// Shift and turning the top platter selects the same "deck.jog" control as turning
    /// it plain does, but this says it was the SEARCH row (jog.top.shift.turn), not the
    /// plain scrub row, that actually fired.</summary>
    public string? ActiveRowId
    {
        get => _activeRowId;
        private set
        {
            if (_activeRowId == value) return;
            _activeRowId = value;
            Raise();
        }
    }

    private string _activeLayerId = "plain";
    /// <summary>Which layer the board currently reads as, driven by whichever modifier
    /// (Shift, the stem-level button, …) is physically held right now. "plain" when
    /// nothing is held.</summary>
    public string ActiveLayerId
    {
        get => _activeLayerId;
        private set
        {
            if (_activeLayerId == value) return;
            _activeLayerId = value;
            Raise();
        }
    }

    /// <summary>Fires with a control id exactly when a live gesture selects a NEW
    /// control — never once per tick of a knob or fader that is already selected. The
    /// view runs the same blink-then-glow animation a click uses (see
    /// FaceplateOverlay.BlinkThenSelect) so a press on the unit and a click on screen
    /// read as one action.</summary>
    public event Action<string>? BlinkRequested;

    /// <summary>Feed a gesture arriving live off the controller (Inspect mode routes every
    /// gesture here instead of — or as well as — the app). Two jobs:
    /// <para>1. A modifier (<c>shift.hold</c>, <c>stemlevel.hold</c>) never selects
    /// anything — a DJ holding Shift to try a chord does not want the panel to jump to
    /// the SHIFT button itself. It only flips <see cref="ActiveLayerId"/>, from the
    /// <c>Pressed</c> flag on the source event, back to "plain" on release.</para>
    /// <para>2. Anything else resolves to the control that owns it
    /// (<see cref="GestureToControl"/>) and selects it — but ONLY when that control (or
    /// deck) actually differs from what's already selected. A knob or fader fires many
    /// gestures a second; reselecting — and so re-blinking — on every one of them would
    /// strobe. While the same knob keeps turning, the panel and the glow simply stay
    /// put, and only <see cref="ActiveRowId"/> keeps following which exact gesture is
    /// firing.</para></summary>
    public void OnLiveGesture(Gesture gesture)
    {
        if (gesture.Id is GestureIds.ShiftHold or GestureIds.StemLevelHold)
        {
            if (TryGetPressed(gesture.Source, out var pressed))
                ActiveLayerId = pressed ? LayerFor(gesture.Id) : "plain";
            return;
        }

        var controlId = ResolveControl(gesture);
        if (controlId is null) return;

        ActiveRowId = gesture.Id;

        var changed = _selected?.Id != controlId || _selectedDeck != gesture.Deck;
        if (!changed) return;

        SelectCore(controlId, gesture.Deck);
        BlinkRequested?.Invoke(controlId);
    }

    /// <summary>Which layer a held modifier's OWNING control names in the document
    /// (<c>LayerDoc.Modifier</c>), or "plain" if none does — belt-and-braces; every
    /// modifier gesture is expected to resolve to a layer.</summary>
    private string LayerFor(string modifierGestureId)
    {
        var ownerControl = GestureToControl.Resolve(Doc, modifierGestureId);
        return ownerControl is null
            ? "plain"
            : Doc.Layers.FirstOrDefault(l => l.Modifier == ownerControl)?.Id ?? "plain";
    }

    private static bool TryGetPressed(ControllerEvent source, out bool pressed)
    {
        switch (source)
        {
            case ControllerEvent.DeckShift ds: pressed = ds.Pressed; return true;
            case ControllerEvent.StemLevelMode sl: pressed = sl.Pressed; return true;
            default: pressed = false; return false;
        }
    }

    /// <summary>The three EQ knobs all answer to the same gesture id ("eq.turn" /
    /// "eq.stemlevel.turn") because the recognizer doesn't mint a separate id per band —
    /// the band lives on the event instead. <see cref="GestureToControl"/> alone can't
    /// tell HI from MID from LOW, so for an EQ gesture this reads the band off the
    /// source event and maps it directly, falling back to the generic resolver for
    /// everything else.</summary>
    private string? ResolveControl(Gesture gesture)
    {
        if (gesture.Source is ControllerEvent.EqMoved eq)
            return eq.Band switch
            {
                EqBand.High => "mixer.eq.hi",
                EqBand.Mid => "mixer.eq.mid",
                EqBand.Low => "mixer.eq.low",
                _ => GestureToControl.Resolve(Doc, gesture.Id),
            };
        return GestureToControl.Resolve(Doc, gesture.Id);
    }

    /// <summary>The pointer entered or left one row. Highlights (and, if it names a
    /// partner, numbers) everything that row involves for as long as the pointer stays
    /// there.</summary>
    public void HoverRow(string? gestureId)
    {
        if (_hoverGestureId == gestureId) return;
        _hoverGestureId = gestureId;
        Raise(nameof(Highlighted));
    }

    /// <summary>Clear the selection and ask the host to dismiss the overlay.</summary>
    public void Close()
    {
        Selected = null;
        SelectedDeck = -1;
        _hoverGestureId = null;
        ActiveRowId = null;
        SelectionChanged?.Invoke(null, -1);
        Raise(nameof(Highlighted));
        RequestClose?.Invoke();
    }

    /// <summary>Drop the selection but leave the overlay up. The host calls this when
    /// Esc is pressed, before deciding whether to close the overlay itself; the backdrop
    /// no longer calls it — a click there is a deliberate no-op now (see
    /// FaceplateOverlay.axaml).</summary>
    public void ClearSelection()
    {
        if (_selected is null) return;
        Selected = null;
        SelectedDeck = -1;
        _hoverGestureId = null;
        ActiveRowId = null;
        SelectionChanged?.Invoke(null, -1);
        Raise(nameof(Highlighted));
    }

    /// <summary>The host closes the overlay when this fires.</summary>
    public event Action? RequestClose;

    /// <summary>Every gesture <paramref name="control"/> is involved in: its own first,
    /// in file order, then any other control's gesture that names it as a partner.</summary>
    private IEnumerable<(GestureDoc Gesture, string OwnerId)> InvolvementFor(ControlDoc control)
    {
        foreach (var g in control.Gestures) yield return (g, control.Id);
        foreach (var (owner, gesture) in _allGestures)
        {
            if (owner.Id == control.Id) continue;   // already covered as owned above
            if (gesture.With is { Count: > 0 } && gesture.With.Contains(control.Id))
                yield return (gesture, owner.Id);
        }
    }

    private static GestureRow ToRow(GestureDoc g, string ownerId) =>
        new(g.Id, ownerId, g.Verb, g.Part, g.Layer, g.Result, g.Used, g.With ?? []);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
