using Sholto.App.Decks;
using Sholto.App.Library.Markers;

namespace Sholto.App.Tests;

/// <summary>Hot cues that do nothing, for tests of something else.</summary>
internal sealed class NullDeckHotCues : IDeckHotCues
{
    public void Attach(IHotCueStore store) { }
    public void Press(int deck, int pad, bool pressed) { }
    public void Delete(int deck, int pad) { }
}
