using Sholto.Audio;

namespace Sholto.Bench.Scenario;

/// <summary>
/// Interprets the deck-level actions (load, gain, play, crossfader, wait) of a
/// <see cref="Scenario"/> against a set of real <see cref="Deck"/> objects. Deck-
/// level because that's the one type every host shares: <c>render</c>/<c>state</c>
/// build theirs via <see cref="Sholto.Bench.Rendering.BenchDeck"/>, and <c>ui</c>
/// drives the same type through <c>MainViewModel.Deck1.Player</c> /
/// <c>Deck2.Player</c> — same class, different owner. This runner never knows
/// which.
///
/// The ui-only actions (scan, key, click, screenshot, gesture, midi) are NOT
/// interpreted here — they need a real window (or, for gesture/midi, the real
/// GestureRecognizer/GestureBus/Orchestrator stack), which render/state never
/// have. <see cref="OnUiAction"/> is invoked for them; hosts that can't act on
/// them leave it null and get a clear <see cref="NotSupportedException"/> instead
/// of a silent no-op.
/// </summary>
public sealed class ScenarioRunner
{
    /// <summary>Advance playback time by this much. For <c>render</c> this pulls
    /// and writes real frames through the mix router; for <c>state</c>/<c>ui</c>
    /// it just pulls each loaded deck's own frames and discards them (the same
    /// "pull, don't wait" trick <c>state</c> already used pre-scenario) so deck
    /// position moves without needing a router or a WAV file.</summary>
    public delegate void AdvanceTime(TimeSpan span);

    private readonly IReadOnlyDictionary<int, Deck> _decks;
    private readonly AdvanceTime _advance;
    private readonly Dictionary<int, float> _channelGain = new();
    private readonly Dictionary<int, float> _crossfadeGain = new();

    /// <summary>Called for key/click/screenshot steps. Null hosts (render, state)
    /// get a <see cref="NotSupportedException"/> naming the action instead of
    /// silently skipping it — a scenario author needs to know their file asked
    /// for something this host cannot do, not have it vanish quietly.</summary>
    public Action<ScenarioAction>? OnUiAction { get; init; }

    public ScenarioRunner(IReadOnlyDictionary<int, Deck> decks, AdvanceTime advance)
    {
        _decks = decks;
        _advance = advance;
    }

    public void Run(Scenario scenario)
    {
        foreach (var action in scenario.Actions) Execute(action);
    }

    private void Execute(ScenarioAction a)
    {
        switch (a.Action)
        {
            case "load":
            {
                var deck = DeckFor(a.Deck!.Value);
                deck.LoadStreaming(a.Track!);
                _channelGain[a.Deck.Value] = (float)(a.Value ?? 1.0);
                ApplyVolume(a.Deck.Value);
                deck.Play();
                // load implicitly starts playback so a bare "load" step (no
                // explicit play step) still produces audible output — matches
                // the pre-scenario render/state CLI flags, which always played.
                break;
            }
            case "gain":
                _channelGain[a.Deck!.Value] = (float)a.Value!.Value;
                ApplyVolume(a.Deck.Value);
                break;
            case "play":
                DeckFor(a.Deck!.Value).Play();
                break;
            case "crossfader":
            {
                // Same equal-power curve as MainViewModel.Crossfader.
                EqualPowerCrossfade.ComputeGains(a.Value!.Value, out float gainA, out float gainB);
                _crossfadeGain[1] = gainA;
                _crossfadeGain[2] = gainB;
                foreach (var deckId in _decks.Keys) ApplyVolume(deckId);
                break;
            }
            case "wait":
            {
                TimeSpan span = a.Seconds is { } s
                    ? TimeSpan.FromSeconds(s)
                    : TimeSpan.FromSeconds(a.Beats!.Value * 60.0 / a.Bpm!.Value);
                _advance(span);
                break;
            }
            case "scan":
            case "key":
            case "click":
            case "screenshot":
            case "gesture":
            case "midi":
                if (OnUiAction is null)
                    throw new NotSupportedException(
                        $"scenario action \"{a.Action}\" needs a real window — only the ui host can drive it.");
                OnUiAction(a);
                break;
            default:
                // ScenarioParser already rejects unknown actions before Run is
                // ever called, so reaching this is a parser/runner mismatch bug,
                // not a bad scenario file.
                throw new InvalidOperationException($"unhandled scenario action \"{a.Action}\" — ScenarioParser should have rejected this");
        }
    }

    private Deck DeckFor(int id) =>
        _decks.TryGetValue(id, out var d) ? d : throw new FormatException($"scenario references deck {id}, which this run doesn't have");

    private void ApplyVolume(int deckId)
    {
        float channel = _channelGain.GetValueOrDefault(deckId, 1.0f);
        float crossfade = _crossfadeGain.GetValueOrDefault(deckId, 1.0f);
        DeckFor(deckId).Volume = channel * crossfade;
    }
}
