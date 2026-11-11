using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>How hot cues are set, supplied via the standard <c>IOptions&lt;HotCueOptions&gt;</c> pipeline.</summary>
public sealed class HotCueOptions
{
    /// <summary>The colour a new hot cue gets.</summary>
    public CueColour DefaultColour { get; set; } = CueColour.Green;

    /// <summary>Snap a new hot cue to the nearest analysed beat (Rekordbox's quantize); off keeps the raw playhead.</summary>
    public bool SnapToBeat { get; set; } = true;
}
