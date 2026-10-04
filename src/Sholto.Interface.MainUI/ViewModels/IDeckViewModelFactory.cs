namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Builds a <see cref="DeckViewModel"/> projecting one deck's events.</summary>
public interface IDeckViewModelFactory
{
    /// <param name="deck">0 for deck 1, 1 for deck 2.</param>
    DeckViewModel Create(int deck);
}
