namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Builds the LOAD TO slot for a deck.</summary>
public interface IDeckSlotFactory
{
    /// <summary>The slot for <paramref name="deck"/> (0-based).</summary>
    IDeckSlot Create(int deck);
}
