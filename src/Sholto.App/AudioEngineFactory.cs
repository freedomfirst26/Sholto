using Sholto.App.Audio;
using SoundFlow.Structs;

namespace Sholto.App;

/// <summary>Constructs the <see cref="AudioEngine"/>; the arguments are passed through
/// unchanged, plus the cue-router factory it was composed with.</summary>
public sealed class AudioEngineFactory(ICueOutputRouterFactory cueRouterFactory) : IAudioEngineFactory
{
    private readonly ICueOutputRouterFactory _cueRouterFactory = cueRouterFactory;

    public AudioEngine Create(IEnumerable<INeedsAudioEngine> engineDependents, IPipeWireRouter pipeWireRouter,
        IControllerSoundCard controllerCard, AudioFormat deckFormat, params IEngineDeck[] decks) =>
        new(engineDependents, pipeWireRouter, controllerCard, _cueRouterFactory, deckFormat, decks);
}
