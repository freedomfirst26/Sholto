using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The strip across the top of Glance: the two deck slots and which deck
/// the list is fitted to.</summary>
public interface IGlanceHeaderViewModel : INotifyPropertyChanged
{
    /// <summary>The reference deck, 0-based; -1 when there is none.</summary>
    int ReferenceDeck { get; }

    /// <summary>The two deck slots, deck 1 then deck 2.</summary>
    IReadOnlyList<IDeckSlotViewModel> Slots { get; }

    /// <summary>Show the result of a ranking made for <paramref name="targetDeck"/>.</summary>
    void Apply(RankedTracks ranked, int targetDeck);

    /// <summary>The overlay opened or closed; the slots tick only while open.</summary>
    void SetOpen(bool isOpen);
}
