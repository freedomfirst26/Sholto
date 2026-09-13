using Sholto.Audio;

namespace Sholto.Bench.Rendering;

/// <summary>
/// The "pull, don't wait" trick <c>state</c> used before scenarios existed:
/// advance each deck's own playback position by pulling frames through its
/// component and discarding them, with no router and no WAV involved. Shared by
/// <c>state</c> and <c>ui</c> scenario hosts, where a "wait" step only needs
/// position to move, not real output.
/// </summary>
public static class DeckAdvance
{
    private const int FramesPerBuffer = OfflineRenderer.FramesPerBuffer;

    public static void Advance(IEnumerable<Deck> decks, TimeSpan span, int channels = 2)
    {
        var deckList = decks as IReadOnlyList<Deck> ?? decks.ToList();
        long totalFrames = (long)(span.TotalSeconds * AudioEngine.DeckFormat.SampleRate);
        var scratch = new float[FramesPerBuffer * channels];

        long framesDone = 0;
        while (framesDone < totalFrames)
        {
            int frames = (int)Math.Min(FramesPerBuffer, totalFrames - framesDone);
            var span2 = scratch.AsSpan(0, frames * channels);
            foreach (var deck in deckList)
                deck.Component.Process(span2, channels);
            framesDone += frames;
        }
    }
}
