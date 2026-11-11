using Sholto.App.Decks;
using Sholto.App.Library.Markers;

namespace Sholto.App.Tests;

/// <summary>Records the hot-cue calls it gets.</summary>
internal sealed class RecordingDeckHotCues : IDeckHotCues
{
    public List<string> Calls { get; } = [];

    public void Attach(IHotCueStore store) { }
    public void Press(int deck, int pad, bool pressed) => Calls.Add($"press {deck} {pad} {pressed}");
    public void Delete(int deck, int pad) => Calls.Add($"delete {deck} {pad}");
}
