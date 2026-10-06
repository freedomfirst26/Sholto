using Sholto.App.Decks;
using Sholto.App.Library;

namespace Sholto.App.Loading;

/// <summary>Remembers the last accepted load for 10 s so it can be undone. Only the last load can be undone.</summary>
public interface ILoadUndo
{
    /// <summary>Capture what the deck holds, just before a load replaces it. Returns the record to pass to
    /// <see cref="Commit"/> once the load is accepted. App thread.</summary>
    LoadRecord Capture(IDeckSession deck, Track incoming);

    /// <summary>The load was accepted: it becomes the one that can be undone.</summary>
    void Commit(LoadRecord record);

    /// <summary>Take the undoable load if it is still within the window, clearing it; null when there is none
    /// or it has lapsed.</summary>
    LoadRecord? Take();
}
