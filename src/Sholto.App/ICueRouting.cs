using Sholto.Data;

namespace Sholto.App;

/// <summary>The App's headphone-cue, master-cue and pad-page state. Each change applies its audio
/// effect, then announces the new state on the bus.</summary>
public interface ICueRouting
{
    /// <summary>Publish the starting state (everything off, both decks on the Hot Cue page), so a
    /// subscriber that joins later is replayed a full picture.</summary>
    void Start();

    /// <summary>Flip a deck's headphone cue.</summary>
    void ToggleHeadphoneCue(int deck);

    /// <summary>Flip MASTER CUE.</summary>
    void ToggleMasterCue();

    /// <summary>Make <paramref name="page"/> the deck's active pad page.</summary>
    void SelectPadPage(int deck, PadPage page);
}
