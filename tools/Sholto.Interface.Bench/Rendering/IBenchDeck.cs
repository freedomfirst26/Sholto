using Sholto.App.Audio;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.Interface.Bench.Rendering;

/// <summary>Builds the offline engine and the engine-attached decks Bench renders and measures through.</summary>
public interface IBenchDeck
{
    /// <summary>A fresh offline engine; opens no playback device.</summary>
    SfEngine CreateEngine();

    /// <summary>A deck attached to <paramref name="engine"/>.</summary>
    Deck Create(SfEngine engine);

    /// <summary>Attaches, loads and starts playback of <paramref name="filePath"/> at <paramref name="gain"/>.</summary>
    Deck LoadAndPlay(SfEngine engine, string filePath, float gain);
}
