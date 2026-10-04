using Sholto.Interface.MainUI.ViewModels;
using Sholto.App.Audio;
using Sholto.Interface.Bench.State;

namespace Sholto.Interface.MainUI.Harness.State;

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

    public UiStateSnapshot() { }

    /// <summary>Snapshots <paramref name="vm"/> and the concrete <paramref name="decks"/> as they are right now.</summary>
    public UiStateSnapshot(MainViewModel vm, IReadOnlyList<Deck> decks)
    {
        SelectedTrackIndex = vm.SelectedTrackIndex;
        TracksCount = vm.Tracks.Count;
        IsSearchOpen = vm.IsSearchOpen;
        Crossfader = vm.Crossfader;
        Decks = [.. decks.Select(d => new DeckStateSnapshot(d))];
    }
}
