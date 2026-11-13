using Sholto.App.Audio;
using Sholto.App.Audio.Recording;
using SoundFlow.Structs;
using Sholto.Data;

namespace Sholto.App;

/// <summary>Constructs the <see cref="AudioEngine"/>; the arguments are passed through
/// unchanged, plus the cue-router factory and the mix recording's master tap it was composed with.</summary>
public sealed class AudioEngineFactory(ICueOutputRouterFactory cueRouterFactory, ILogFactory logs, IMasterTap masterTap) : IAudioEngineFactory
{
    private readonly ICueOutputRouterFactory _cueRouterFactory = cueRouterFactory;
    private readonly ILogFactory _logs = logs;
    private readonly IMasterTap _masterTap = masterTap;

    public AudioEngine Create(IEnumerable<INeedsAudioEngine> engineDependents, IPipeWireRouter pipeWireRouter,
        IControllerSoundCard controllerCard, AudioFormat deckFormat, params IEngineDeck[] decks) =>
        new(engineDependents, pipeWireRouter, controllerCard, _cueRouterFactory, _masterTap, deckFormat, _logs.Create("AudioEngine"), decks);
}
