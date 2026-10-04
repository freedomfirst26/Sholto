using Sholto.App.Audio;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>Advances each deck's playback position by pulling frames through its component and discarding them.</summary>
public interface IDeckAdvance
{
    void Advance(IEnumerable<Deck> decks, TimeSpan span, int channels = 2);
}
