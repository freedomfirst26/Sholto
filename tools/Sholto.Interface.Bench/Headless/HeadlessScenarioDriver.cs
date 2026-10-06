using Sholto.App;
using Sholto.App.Audio;
using Sholto.Data;
using Sholto.Interface.Bench.Behaviour;
using Sholto.Interface.Bench.Controller;
using Sholto.Interface.Bench.Scenario;
using Sholto.Interface.Controller;

namespace Sholto.Interface.Bench.Headless;

/// <summary>
/// Handles the non-deck scenario actions a headless run can do (scan, gesture, midi) that
/// <see cref="ScenarioRunner"/> hands off via <c>OnUiAction</c>. Gesture and midi steps become
/// controller events through the real input stack, and so Commands on the bus; a small set of observable
/// core/deck fields is snapshotted before and after, and only the fields that actually changed are
/// recorded — an empty <c>Changed</c> means the step ran but visibly moved nothing, which is the
/// finding a harness must not hide. key / click / screenshot need a window and are refused here: they
/// belong to the MainUI harness tool.
/// </summary>
/// <param name="core">The headless core, scanned and read directly.</param>
/// <param name="gestures">Sends controller events through the real input stack.</param>
/// <param name="decks">The concrete Deck1 and Deck2 (index 0 and 1), for the internal members the ports do not expose.</param>
/// <param name="gestureFactory">Turns a "gesture" step into the <c>ControllerEvent</c> it names.</param>
/// <param name="snapshot">Reads the observable fields.</param>
/// <param name="diff">Finds which of them changed.</param>
public sealed class HeadlessScenarioDriver(CoreStack core, GestureHost gestures, IReadOnlyList<Deck> decks,
    IScenarioGestureFactory gestureFactory, ICoreSnapshot snapshot, IStateDiff diff)
{
    private readonly CoreStack _core = core;
    private readonly GestureHost _gestures = gestures;
    private readonly IScenarioGestureFactory _gestureFactory = gestureFactory;
    private readonly ICoreSnapshot _snapshot = snapshot;
    private readonly IStateDiff _diff = diff;
    private readonly TimeSpan LoadSettleTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The concrete Deck1 and Deck2 (index 0 and 1) this driver snapshots.</summary>
    public IReadOnlyList<Deck> Decks { get; } = decks;

    public List<InteractionOutcome> Outcomes { get; } = [];
    public List<GestureOutcome> GestureOutcomes { get; } = [];

    public void Handle(ScenarioAction a)
    {
        switch (a.Action)
        {
            case "wait":
                // The deck position has already been advanced by the runner; let due gestures land.
                _gestures.Pump();
                break;
            case "scan":
                Outcomes.Add(Scan(a));
                break;
            case "gesture":
                GestureOutcomes.Add(Gesture(a));
                break;
            case "midi":
                GestureOutcomes.Add(Midi(a));
                break;
            default:
                throw new NotSupportedException(
                    $"scenario action \"{a.Action}\" needs a window — run it with the MainUI harness (the MainUI harness tool), not the headless bench.");
        }
    }

    private InteractionOutcome Scan(ScenarioAction a)
    {
        var before = _snapshot.Take(_core, Decks);
        // The app thread is immediate here, so the scan's final "apply to the rows" step runs on the
        // scan's own thread; there is no dispatcher to pump.
        _core.Library.ScanAsync(a.Dir!, null).GetAwaiter().GetResult();
        var after = _snapshot.Take(_core, Decks);
        return new InteractionOutcome { Action = "scan", Description = $"scan {a.Dir}", Changed = _diff.Between(before, after) };
    }

    /// <summary>Build the named ControllerEvent and send it through the real pipeline
    /// (<see cref="GestureHost.SendGesture"/>), diffing around it.</summary>
    private GestureOutcome Gesture(ScenarioAction a)
    {
        var evt = _gestureFactory.Create(a);
        var before = _snapshot.Take(_core, Decks);
        _gestures.SendGesture(evt);
        AwaitLoadsSettled();
        var after = _snapshot.Take(_core, Decks);
        return new GestureOutcome
        {
            Action = "gesture",
            Description = $"gesture {a.Event} deck={a.Deck}",
            ControllerEvent = evt.ToString(),
            Changed = _diff.Between(before, after),
        };
    }

    /// <summary>A load gesture starts decoding on a pool thread and the deck only flips to loaded when that
    /// finishes; wait (bounded) for any deck still <see cref="DeckLoadState.Loading"/> so the "after"
    /// snapshot sees the result.</summary>
    private void AwaitLoadsSettled()
    {
        for (int i = 0; i < 2; i++)
        {
            var deck = _core.Decks.DeckFor(i);
            var deadline = DateTime.UtcNow + LoadSettleTimeout;
            while (deck.LoadState == DeckLoadState.Loading)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"deck {i + 1} was still loading after {LoadSettleTimeout.TotalSeconds:0}s");
                Thread.Sleep(2);
            }
        }
    }

    /// <summary>Translate the raw note/CC through the FLX-4 mapping — one layer above
    /// <see cref="Gesture"/> — and, if it resolved, send the result through the same pipeline.
    /// <see cref="GestureOutcome.ControllerEvent"/> is null when the wire number is unmapped, same as
    /// it would be on real hardware.</summary>
    private GestureOutcome Midi(ScenarioAction a)
    {
        var before = _snapshot.Take(_core, Decks);
        ControllerEvent? evt = a.Note is { } note
            ? _gestures.SendMidiNote(a.MidiChannel!.Value, note, a.Velocity ?? (a.IsDown ?? true ? 127 : 0), a.IsDown ?? true)
            : _gestures.SendMidiCc(a.MidiChannel!.Value, a.Cc!.Value, a.CcValue!.Value);
        var after = _snapshot.Take(_core, Decks);
        string what = a.Note is { } n ? $"note 0x{n:X2}" : $"cc 0x{a.Cc:X2}={a.CcValue}";
        return new GestureOutcome
        {
            Action = "midi",
            Description = $"midi ch={a.MidiChannel} {what}",
            ControllerEvent = evt?.ToString(),
            Changed = _diff.Between(before, after),
        };
    }
}
