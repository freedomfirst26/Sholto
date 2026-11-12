using Sholto.App.Audio;
using SoundFlow.Structs;
using Sholto.Data;

namespace Sholto.App;

/// <summary>Constructs the <see cref="AudioEngine"/>; the arguments are passed through
/// unchanged, plus the cue-router factory it was composed with.</summary>
public sealed class AudioEngineFactory(ICueOutputRouterFactory cueRouterFactory, ILogFactory logs) : IAudioEngineFactory
{
    private readonly ICueOutputRouterFactory _cueRouterFactory = cueRouterFactory;
    private readonly ILogFactory _logs = logs;

    public AudioEngine Create(IEnumerable<INeedsAudioEngine> engineDependents, IPipeWireRouter pipeWireRouter,
        IControllerSoundCard controllerCard, AudioFormat deckFormat, params IEngineDeck[] decks) =>
        new(engineDependents, pipeWireRouter, controllerCard, _cueRouterFactory, deckFormat, decks);
}
