using System.ComponentModel;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The strip across the top of Glance: its key and BPM, its title
/// and countdown.</summary>
public interface IGlanceHeaderViewModel : INotifyPropertyChanged
{
    /// <summary>A deck is the fit reference.</summary>
    bool HasReference { get; }

    /// <summary>The reference deck, 0-based; -1 when there is none.</summary>
    int ReferenceDeck { get; }

    bool IsPlaying { get; }

    /// <summary>Camelot code of the reference, "8A".</summary>
    string KeyText { get; }

    /// <summary>BPM of the reference to one decimal, "130.0".</summary>
    string BpmText { get; }

    /// <summary>Title of the reference deck's track.</summary>
    string Title { get; }

    /// <summary>"−2:30" while the reference plays; empty otherwise.</summary>
    string CountdownText { get; }

    /// <summary>Under 45 seconds left.</summary>
    bool IsLow { get; }

    /// <summary>The two LOAD TO slots, deck 1 then deck 2.</summary>
    IReadOnlyList<IDeckSlotViewModel> Slots { get; }

    /// <summary>The active library filter ("📦 Peak time"), or null when the whole library shows.</summary>
    string? FilterLabel { get; }

    /// <summary>Show the result of a ranking made for <paramref name="targetDeck"/>.</summary>
    void Apply(RankedTracks ranked, int targetDeck);

    /// <summary>The overlay opened or closed; the countdown ticks only while open.</summary>
    void SetOpen(bool isOpen);
}
