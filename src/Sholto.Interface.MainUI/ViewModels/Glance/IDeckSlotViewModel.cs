using System.ComponentModel;
using Avalonia.Media;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>One of the two LOAD TO slots in the Glance header: a platter that spins while its deck plays (an empty deck shows a ghost record half out of the slot's left edge) with
/// a ring of the time left, the deck's title and, on the second line, its key and tempo, or the warning
/// a load would raise. The slot is the target (a load goes here), the fit reference, in caution (the target is
/// playing) or armed (the first replace press is made).</summary>
public interface IDeckSlotViewModel : INotifyPropertyChanged
{
    /// <summary>The deck's number as the person knows it, 1 or 2.</summary>
    int Number { get; }

    bool IsLoaded { get; }

    bool IsPlaying { get; }

    /// <summary>A load goes to this deck.</summary>
    bool IsTarget { get; }

    /// <summary>The other deck is the target and this one is what the list is fitted to.</summary>
    bool IsReference { get; }

    /// <summary>The target deck is playing, so a load would replace it. False once the replace is armed.</summary>
    bool IsCaution { get; }

    /// <summary>The first replace press is made and a second within 3 s replaces the playing deck.</summary>
    bool IsArmed { get; }

    /// <summary>The track's title, or "Empty".</summary>
    string Title { get; }

    /// <summary>Camelot code, "8A"; empty before key analysis lands.</summary>
    string Camelot { get; }

    /// <summary>The deck's own key colour, drawn behind <see cref="Camelot"/>.</summary>
    IBrush? KeyBrush { get; }

    /// <summary>The tempo heard to one decimal, "128.0"; empty when the track has none.</summary>
    string BpmText { get; }

    /// <summary>The slot is loaded, playing and has under 45 s left at the deck's speed: the ring turns red.</summary>
    bool IsLow { get; }

    /// <summary>"Playing": the caution line, without a time.</summary>
    string CautionText { get; }

    /// <summary>A loaded slot shows its key cap.</summary>
    bool ShowsKeyCap { get; }

    /// <summary>"⏎ ⇧1" on the target deck, otherwise "⇧1" or "⇧2".</summary>
    string KeyCapLabel { get; }

    /// <summary>"⇧2 again to replace": the armed line.</summary>
    string ArmedText { get; }

    /// <summary>"DECK 2 · empty": the empty slot's first line, whole.</summary>
    string EmptyTitle { get; }

    /// <summary>"Loads here" on the target, "to load" otherwise: the empty slot's second line, after the load arrow
    /// or the key cap.</summary>
    string EmptyHint { get; }

    /// <summary>"⇧2": the key cap before "to load" on a non-target empty slot.</summary>
    string KeyCapText { get; }

    /// <summary>The slot is the empty target, and motion is allowed: a soft light sweeps across it. Never under
    /// reduced motion.</summary>
    bool IsSweeping { get; }

    /// <summary>Line two shows key and tempo.</summary>
    bool ShowStats { get; }

    bool ShowCaution { get; }

    bool ShowArmed { get; }

    /// <summary>The share of the track still to play, 0 to 1, in whole steps of the ring. Moves with <see cref="Changed"/>.</summary>
    double RemainingFraction { get; }

    /// <summary>How far the platter has turned, 0 up to (not including) 1, in whole degrees. One turn is one bar.
    /// Stays at 0 under reduced motion. Moves with <see cref="Changed"/>.</summary>
    double SpinTurns { get; }

    /// <summary>Raised when something the platter draws moved: a state, a ring step or a degree of spin.</summary>
    event Action? Changed;
}
