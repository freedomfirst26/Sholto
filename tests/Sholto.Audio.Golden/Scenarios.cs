using Sholto.Audio;
using Sholto.Bench.Rendering;

namespace Sholto.Audio.Golden;

/// <summary>
/// The fixed, scripted timelines the golden tests render. Each one builds its
/// own fresh <see cref="BenchDeck"/>-wired deck(s) on a fresh offline engine
/// (see <see cref="BenchDeck.CreateEngine"/> — constructing it opens no audio
/// device), loads a <see cref="Signal"/> tone stack directly via
/// <see cref="Deck.Load"/> (bypasses file decode entirely — see that method's
/// own doc; it's a real production path, used by <c>DeckViewModel.LoadTrack</c>
/// for drag-and-drop, not a test-only shortcut), then scripts deck mutations
/// between <see cref="RenderSession.Advance"/> calls exactly the way a human
/// would twist a knob between two moments in a set.
///
/// Kept as static "record this timeline" methods, separate from the assert
/// step, so both <see cref="GoldenAudioTests"/> (compare against a checked-in
/// reference) and <see cref="DeterminismTests"/> (compare two renders of the
/// SAME timeline against each other) can call the identical code.
/// </summary>
internal static class Scenarios
{
    /// <summary>Gain (deck1 fader down) then an equal-power crossfade from
    /// deck1 to deck2 — the same cosine/sine curve
    /// <c>Sholto.Bench.Scenario.ScenarioRunner</c> uses for its "crossfader"
    /// action, applied here directly via <see cref="Deck.Volume"/> since
    /// that's all the curve actually does to a deck.</summary>
    public static void RenderGainAndCrossfade(string outWavPath)
    {
        var engine = BenchDeck.CreateEngine();
        var deck1 = BenchDeck.Create(engine);
        var deck2 = BenchDeck.Create(engine);
        deck1.Load("golden:deckA", Signal.ToneStackA(2.0), Signal.SampleRate);
        deck2.Load("golden:deckB", Signal.ToneStackB(2.0), Signal.SampleRate);
        deck1.Play();
        deck2.Play();

        using var session = new RenderSession([deck1, deck2], engine, AudioEngine.DeckFormat, outWavPath);

        deck1.Volume = 1.0f; deck2.Volume = 0.0f;
        session.Advance(TimeSpan.FromSeconds(0.15)); // deck1 alone, full gain

        deck1.Volume = 0.35f;
        session.Advance(TimeSpan.FromSeconds(0.15)); // deck1 alone, faded down

        const float centre = 0.70710678f; // cos(pi/4) == sin(pi/4)
        deck1.Volume = centre; deck2.Volume = centre;
        session.Advance(TimeSpan.FromSeconds(0.15)); // crossfader centred, both decks

        deck1.Volume = 0.0f; deck2.Volume = 1.0f;
        session.Advance(TimeSpan.FromSeconds(0.15)); // crossfaded fully to deck2
    }

    /// <summary>Neutral, then each of the 3 isolator bands killed in turn,
    /// then the high band boosted — <see cref="Deck.SetEq"/>'s full input
    /// range (0 = kill, 0.5 = unity, 1 = +6 dB).</summary>
    public static void RenderEqBands(string outWavPath)
    {
        var engine = BenchDeck.CreateEngine();
        var deck = BenchDeck.Create(engine);
        deck.Load("golden:eq", Signal.ToneStackA(1.5), Signal.SampleRate);
        deck.Play();

        using var session = new RenderSession([deck], engine, AudioEngine.DeckFormat, outWavPath);
        var step = TimeSpan.FromSeconds(0.12);

        session.Advance(step); // neutral (construction default: all bands unity)

        deck.SetEq(0, 0.0); session.Advance(step); // low killed
        deck.SetEq(0, 0.5); deck.SetEq(1, 0.0); session.Advance(step); // mid killed
        deck.SetEq(1, 0.5); deck.SetEq(2, 0.0); session.Advance(step); // high killed
        deck.SetEq(2, 1.0); session.Advance(step); // high boosted +6dB
    }

    /// <summary>Bypass → full low-pass → bypass → full high-pass —
    /// <see cref="Deck.SetFilter"/>'s full sweep range, in both directions so
    /// the dead-zone bypass reset (see <c>DjFilter.DesignForPosition</c>) is
    /// exercised on the way back through center too.</summary>
    public static void RenderFilterSweep(string outWavPath)
    {
        var engine = BenchDeck.CreateEngine();
        var deck = BenchDeck.Create(engine);
        deck.Load("golden:filter", Signal.ToneStackA(1.5), Signal.SampleRate);
        deck.Play();

        using var session = new RenderSession([deck], engine, AudioEngine.DeckFormat, outWavPath);
        var step = TimeSpan.FromSeconds(0.12);

        deck.SetFilter(0.5); session.Advance(step); // bypass baseline
        deck.SetFilter(0.0); session.Advance(step); // full LP
        deck.SetFilter(0.5); session.Advance(step); // back to bypass
        deck.SetFilter(1.0); session.Advance(step); // full HP
    }

    /// <summary>Off → on (long enough to hear at least one full repeat at the
    /// default 128 BPM half-beat delay, ~0.469s) → off again (tail ringing
    /// out, NOT a bypass — see <see cref="EchoEffect"/>'s class doc).</summary>
    public static void RenderEchoOnOff(string outWavPath)
    {
        var engine = BenchDeck.CreateEngine();
        var deck = BenchDeck.Create(engine);
        deck.Load("golden:echo", Signal.ToneStackA(1.5), Signal.SampleRate);
        deck.Play();

        using var session = new RenderSession([deck], engine, AudioEngine.DeckFormat, outWavPath);

        session.Advance(TimeSpan.FromSeconds(0.1)); // baseline, echo off
        deck.SetEcho(true);
        session.Advance(TimeSpan.FromSeconds(0.5)); // echo feeding — at least one repeat lands
        deck.SetEcho(false);
        session.Advance(TimeSpan.FromSeconds(0.4)); // tail ringing out, no new input
    }

    /// <summary>Forward → reverse → double-speed forward → released, via
    /// <see cref="Deck.ScratchRate"/>/<see cref="Deck.EndScratch"/>. Only
    /// reachable because <see cref="Deck.Load"/> (not LoadStreaming) sets up a
    /// <c>ScratchDataProvider</c> — see <see cref="Deck.CanScratch"/>.</summary>
    public static void RenderScratch(string outWavPath)
    {
        var engine = BenchDeck.CreateEngine();
        var deck = BenchDeck.Create(engine);
        deck.Load("golden:scratch", Signal.ToneStackA(2.0), Signal.SampleRate);
        deck.Play();
        if (!deck.CanScratch)
            throw new InvalidOperationException("golden scratch scenario: deck.CanScratch is false — Deck.Load should have set up a scratchable provider");

        using var session = new RenderSession([deck], engine, AudioEngine.DeckFormat, outWavPath);

        session.Advance(TimeSpan.FromSeconds(0.1)); // normal forward playback
        deck.ScratchRate(-1.0);
        session.Advance(TimeSpan.FromSeconds(0.15)); // reverse
        deck.ScratchRate(2.0);
        session.Advance(TimeSpan.FromSeconds(0.15)); // double-speed forward
        deck.EndScratch();
        session.Advance(TimeSpan.FromSeconds(0.1)); // released, back to normal speed
    }
}
