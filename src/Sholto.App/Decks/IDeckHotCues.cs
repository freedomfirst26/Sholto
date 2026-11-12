using Sholto.App.Library.Markers;

namespace Sholto.App.Decks;

/// <summary>The eight hot-cue pads of each deck: set, jump (with a held preview while paused) and delete, kept in
/// step with what is saved for the deck's track. Publishes <c>DeckHotCuesChanged</c>. Needs the database; until
/// <see cref="Attach"/> it does nothing.</summary>
public interface IDeckHotCues
{
    /// <summary>The database is up: from now on a track that finishes loading gets its saved hot cues.</summary>
    void Attach(IHotCueStore store);

    /// <summary>A hot-cue pad (0-7) went down or up. Down on an empty pad saves a cue at the playhead; down on a
    /// saved pad jumps to it (paused: plays while held, then pauses back on the cue at release, unless
    /// <see cref="LatchPreview"/> took the preview over first). Nothing happens with no track loaded.</summary>
    void Press(int deck, int pad, bool pressed);

    /// <summary>PLAY was pressed on the deck. While a hot-cue preview is in progress (a pad held on a deck that was
    /// paused) this ends the preview without stopping it: the deck keeps playing, and the pad's release no longer
    /// pauses or seeks back to the cue. Returns true when it did, so PLAY does nothing else; false when the deck had
    /// no preview in progress, so PLAY behaves as usual.</summary>
    bool LatchPreview(int deck);

    /// <summary>Delete the cue on a pad; nothing happens when it is empty.</summary>
    void Delete(int deck, int pad);
}
