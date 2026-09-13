namespace Sholto.Audio.Golden;

/// <summary>
/// Golden-audio regression tests: each renders a fixed, synthesised timeline
/// (see <see cref="Scenarios"/>) through the REAL mix path — Sholto.Bench's
/// <c>RenderSession</c>/<c>OfflineRenderer</c> driving
/// <c>CueOutputRouter.Process</c>, the exact per-buffer pull the real audio
/// device callback makes, with no sound card involved (see
/// <c>BenchDeck.CreateEngine</c>'s doc) — and diffs the result against a
/// checked-in reference WAV under Fixtures/.
///
/// EXACTNESS: comparison is bit-exact float32 equality, not a tolerance. See
/// <see cref="DeterminismTests"/> for why exactness is achievable here (every
/// covered path has been proven byte-identical run to run, same process AND
/// across process boundaries) — if it weren't, this whole test design would
/// be unsound, which is why that proof comes first and is asserted
/// separately rather than assumed.
///
/// COVERAGE — what these 5 references exercise, and what they deliberately
/// do NOT:
///   - gain / crossfade   : Deck.Volume, including the equal-power crossfade
///                          curve Sholto.Bench.Scenario.ScenarioRunner uses.
///   - EQ bands           : Deck.SetEq, all 3 bands, kill + boost.
///   - filter sweep       : Deck.SetFilter, full LP and full HP, including
///                          the bypass dead-zone crossing both directions.
///   - echo on/off        : Deck.SetEcho, including the tail-ring-out (NOT a
///                          bypass) behaviour when turned off.
///   - scratch            : Deck.ScratchRate/EndScratch — reverse and
///                          double-speed, reachable because Deck.Load (not
///                          LoadStreaming) sets up a scratchable provider.
///
/// NOT covered — beat loops and stem playback. Both require the deck's
/// stem-mix provider (see DeckLooping's class doc: "v1 only supports the
/// stem path"), which only exists after Deck's real StemAnalyzer produces 4
/// stem files AND Deck's real IAudioFileDecoder decodes them
/// (Deck.SwitchToStemMode). BenchDeckFactory wires BOTH of those to no-ops
/// (NoOpStemAnalyzer always throws, NoOpAudioFileDecoder always throws) —
/// intentionally, per its own class doc, since Bench was never meant to
/// exercise analysis. Making loops/stems reachable would mean giving this
/// test's own deck REAL stem-analysis and decode collaborators (four
/// synthesized stem WAVs, a real decoder) instead of Bench's, which also
/// reintroduces the exact background-thread stem-swap race
/// (Deck.SwitchToStemMode running on a Task.Run) this test suite is
/// otherwise careful to avoid — see DeterminismTests. Left out rather than
/// shipped as a green test that silently exercises nothing.
/// </summary>
public sealed class GoldenAudioTests
{
    [Fact]
    public void GainAndCrossfade_MatchesReference()
    {
        string outPath = TempWav();
        try
        {
            Scenarios.RenderGainAndCrossfade(outPath);
            GoldenReference.AssertMatches(GoldenReference.FixturesDir(), "gain_crossfade.wav", outPath);
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void EqBands_MatchReference()
    {
        string outPath = TempWav();
        try
        {
            Scenarios.RenderEqBands(outPath);
            GoldenReference.AssertMatches(GoldenReference.FixturesDir(), "eq.wav", outPath);
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void FilterSweep_MatchesReference()
    {
        string outPath = TempWav();
        try
        {
            Scenarios.RenderFilterSweep(outPath);
            GoldenReference.AssertMatches(GoldenReference.FixturesDir(), "filter.wav", outPath);
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void EchoOnOff_MatchesReference()
    {
        string outPath = TempWav();
        try
        {
            Scenarios.RenderEchoOnOff(outPath);
            GoldenReference.AssertMatches(GoldenReference.FixturesDir(), "echo.wav", outPath);
        }
        finally { File.Delete(outPath); }
    }

    [Fact]
    public void Scratch_MatchesReference()
    {
        string outPath = TempWav();
        try
        {
            Scenarios.RenderScratch(outPath);
            GoldenReference.AssertMatches(GoldenReference.FixturesDir(), "scratch.wav", outPath);
        }
        finally { File.Delete(outPath); }
    }

    private static string TempWav() =>
        Path.Combine(Path.GetTempPath(), $"sholto-golden-{Guid.NewGuid():N}.wav");
}
