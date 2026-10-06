using System.ComponentModel;
using Sholto.Interface.MainUI.Controls.WaveformStyles;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>Which waveform style the decks draw. Presentation state, owned by MainUI; the App only
/// persists the chosen id (<c>ChooseWaveformStyle</c> → settings, <c>SavedWaveformStyleFound</c> back on
/// startup), exactly as it does for the theme.
///
/// Two values, because the Layout Wizard previews live: <see cref="Shown"/> is what the decks draw right
/// now; <see cref="Chosen"/> is what the user committed to (and what is saved). Cancelling the wizard
/// previews <see cref="Chosen"/> again.</summary>
public interface IWaveformStyleViewModel : INotifyPropertyChanged
{
    /// <summary>Every style on offer, in the order the wizard shows them.</summary>
    IReadOnlyList<IWaveformStyleStrategy> All { get; }

    /// <summary>What the decks draw now. Raises PropertyChanged when it changes.</summary>
    IWaveformStyleStrategy Shown { get; }

    /// <summary>What the user committed to; the saved choice.</summary>
    IWaveformStyleStrategy Chosen { get; }

    /// <summary>Show a style on the decks without choosing it (the wizard's live preview / cancel).</summary>
    void Preview(IWaveformStyleStrategy style);

    /// <summary>The user's choice: show it, and when it differs from <see cref="Chosen"/> tell the App so it
    /// is remembered.</summary>
    void Choose(IWaveformStyleStrategy style);

    /// <summary>Apply the id saved last time (unknown ids fall back to the default). Not a new choice, so
    /// nothing is sent back to the App.</summary>
    void Restore(string savedId);
}
