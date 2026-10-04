using Sholto.App.Audio;
using Sholto.App.Decks;
using SoundFlow.Structs;

namespace Sholto.App.Lifecycle;

/// <summary>See <see cref="IAudioOutput"/>.</summary>
public sealed class AudioOutput(
    IAudioEngineFactory engineFactory,
    IReadOnlyList<INeedsAudioEngine> engineDependents,
    IPipeWireRouter pipeWireRouter,
    IControllerSoundCard controllerSoundCard,
    AudioFormat deckFormat,
    IDecks decks,
    IMasterCueEngineSink masterCue) : IAudioOutput
{
    private readonly IAudioEngineFactory _engineFactory = engineFactory;
    private readonly IReadOnlyList<INeedsAudioEngine> _engineDependents = engineDependents;
    private readonly IPipeWireRouter _pipeWireRouter = pipeWireRouter;
    private readonly IControllerSoundCard _controllerSoundCard = controllerSoundCard;
    private readonly AudioFormat _deckFormat = deckFormat;
    private readonly IDecks _decks = decks;
    private readonly IMasterCueEngineSink _masterCue = masterCue;
    private AudioEngine? _engine;

    public Task StartAsync(string? deviceName) => Task.Run(() =>
    {
        try
        {
            var engine = CreateEngine();
            engine.Start(deviceName);
            Attach(engine);
            Console.WriteLine($"Audio engine started; master speaker={(deviceName ?? "(controller, no separate speaker chosen)")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio engine failed to start: {ex.Message}");
        }
    });

    public Task SwitchAsync(string deviceName) => Task.Run(() =>
    {
        try
        {
            if (_engine is null)
            {
                var engine = CreateEngine();
                engine.Start(deviceName);
                Attach(engine);
            }
            else
            {
                _engine.SwitchDevice(deviceName);
            }
            Console.WriteLine($"Audio engine switched to: {deviceName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio engine failed to start: {ex.Message}");
        }
    });

    public void Stop() => _engine?.Stop();

    private AudioEngine CreateEngine() =>
        _engineFactory.Create(_engineDependents, _pipeWireRouter, _controllerSoundCard, _deckFormat,
            _decks.Deck1.Engine, _decks.Deck2.Engine);

    private void Attach(AudioEngine engine)
    {
        _engine = engine;
        _masterCue.Attach(engine);
    }
}
