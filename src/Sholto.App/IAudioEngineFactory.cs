using Sholto.App.Audio;
using SoundFlow.Structs;

namespace Sholto.App;

/// <summary>Creates the <see cref="AudioEngine"/> over the two decks. Called at first
/// start and again when the engine has to be rebuilt after an output-device change.</summary>
public interface IAudioEngineFactory
{
    AudioEngine Create(IEnumerable<INeedsAudioEngine> engineDependents, IPipeWireRouter pipeWireRouter,
        IControllerSoundCard controllerCard, AudioFormat deckFormat, params IEngineDeck[] decks);
}
