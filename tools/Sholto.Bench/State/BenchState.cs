namespace Sholto.Bench.State;

/// <summary>
/// Top-level state dump: one snapshot per deck plus the crossfader position
/// the scenario applied. Bench composes its own scenarios (it doesn't drive
/// the live app — that's phase 1b), so the crossfader value is whatever the
/// harness fed into <c>Deck.Volume</c> for this run, not a value read off a
/// running ViewModel.
/// </summary>
public sealed class BenchState
{
    public IReadOnlyList<DeckStateSnapshot> Decks { get; init; } = [];
    public double? CrossfaderPosition { get; init; }
}
