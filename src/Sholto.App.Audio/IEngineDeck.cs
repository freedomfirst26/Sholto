using SoundFlow.Structs;
using SfEngine = SoundFlow.Abstracts.AudioEngine;

namespace Sholto.App.Audio;

/// <summary>What <see cref="AudioEngine"/> needs of a deck: a mix source it can hand the
/// SoundFlow engine and output format to once the engine exists.</summary>
public interface IEngineDeck : IMixSource
{
    /// <summary>Attach the deck's audio chain to the engine. Called once, from the
    /// <see cref="AudioEngine"/> constructor.</summary>
    void AttachEngine(SfEngine engine, AudioFormat format);
}
