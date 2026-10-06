namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>A LOAD TO slot as the header drives it: told its roles and the time, it reads its deck and
/// updates what the view binds to.</summary>
public interface IDeckSlot : IDeckSlotViewModel
{
    /// <summary>Re-read the deck at <paramref name="now"/> and take these roles. <paramref name="replacePending"/>
    /// is the App's "press again to replace" state for this deck.</summary>
    void Update(bool isTarget, bool isReference, bool replacePending, DateTime now);
}
