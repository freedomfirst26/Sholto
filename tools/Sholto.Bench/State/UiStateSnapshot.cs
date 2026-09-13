using Sholto.App.ViewModels;

namespace Sholto.Bench.State;

/// <summary>
/// JSON snapshot of what the mounted <c>MainViewModel</c> believes right now,
/// for the <c>ui</c> subcommand — the VM-level counterpart to
/// <see cref="DeckStateSnapshot"/>, which only knows about a single deck.
/// </summary>
public sealed class UiStateSnapshot
{
    public int SelectedTrackIndex { get; init; }
    public int TracksCount { get; init; }
    public bool IsSearchOpen { get; init; }
    public double Crossfader { get; init; }
    public IReadOnlyList<DeckStateSnapshot> Decks { get; init; } = [];

    public static UiStateSnapshot From(MainViewModel vm) => new()
    {
        SelectedTrackIndex = vm.SelectedTrackIndex,
        TracksCount = vm.Tracks.Count,
        IsSearchOpen = vm.IsSearchOpen,
        Crossfader = vm.Crossfader,
        Decks = [DeckStateSnapshot.From(vm.Deck1.Player), DeckStateSnapshot.From(vm.Deck2.Player)],
    };
}
