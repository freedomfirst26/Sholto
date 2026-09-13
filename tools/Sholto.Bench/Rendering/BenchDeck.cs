using Sholto.Audio;
using SoundFlow.Backends.MiniAudio;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Bench.Rendering;

/// <summary>
/// Builds a real <see cref="Deck"/> wired with the no-op fakes from
/// <see cref="DeckFakes"/> and attached to a shared offline engine — the same
/// object the live app plays through, just never connected to a sound card.
/// </summary>
public static class BenchDeck
{
    /// <summary>A fresh <see cref="MiniAudioEngine"/>. Constructing it does not
    /// open any playback device — that only happens if something calls
    /// <c>InitializePlaybackDevice</c>, which Bench never does. This is the
    /// finding the whole harness rests on: rendering needs the engine object,
    /// not a device.</summary>
    public static SfEngine CreateEngine() => new MiniAudioEngine();

    public static Deck Create(SfEngine engine)
    {
        var deck = BenchDeckFactory.Instance.Create();
        deck.AttachEngine(engine, AudioEngine.DeckFormat);
        return deck;
    }

    /// <summary>Attaches, loads and starts playback of <paramref name="filePath"/>
    /// at the given master-path gain (see <see cref="Deck.Volume"/> — it writes
    /// <see cref="Deck.MasterGain"/> directly).</summary>
    public static Deck LoadAndPlay(SfEngine engine, string filePath, float gain)
    {
        var deck = Create(engine);
        deck.Volume = gain;
        deck.LoadStreaming(filePath);
        deck.Play();
        return deck;
    }
}
