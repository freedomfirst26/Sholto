using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>The library-browse knob's selection: which visible row is highlighted. One of the real roles
/// extracted from the old <c>IDeckHost</c>; <see cref="ILibrarySession"/> implements it.</summary>
public interface ITrackSelection
{
    /// <summary>Index of the highlighted row in the visible rows, or -1. May lie outside the rows after a
    /// filter shrinks them; <see cref="SelectedTrack"/> is then null.</summary>
    int SelectedIndex { get; }

    /// <summary>The highlighted track, or null when the index is outside the visible rows.</summary>
    Track? SelectedTrack { get; }

    /// <summary>The highlighted summary, or null.</summary>
    TrackSummary? SelectedSummary { get; }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes.</summary>
    event Action? SelectedIndexChanged;

    /// <summary>Set the index as given (the list box reports -1 when its items are cleared).</summary>
    void SetSelectedIndex(int index);

    /// <summary>Highlight row <paramref name="index"/>, clamped to the visible rows. No rows: nothing happens.</summary>
    void Select(int index);

    /// <summary>The browse knob turned by <paramref name="delta"/> clicks.</summary>
    void Rotate(int delta);
}
