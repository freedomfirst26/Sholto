using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.Data;
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

    public Task<MasterRoute> StartAsync(string? deviceName) => Task.Run(() =>
    {
        try
        {
            var engine = CreateEngine();
            var route = engine.Start(deviceName);
            Attach(engine);
            Console.WriteLine("Audio engine started");
            LogRoute(route);
            return route;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            return FailedToStart(ex);
        }
    });

    public Task<MasterRoute> SwitchAsync(string deviceName) => Task.Run(() =>
    {
        try
        {
            MasterRoute route;
            if (_engine is null)
            {
                var engine = CreateEngine();
                route = engine.Start(deviceName);
                Attach(engine);
            }
            else
            {
                route = _engine.SwitchDevice(deviceName);
            }
            Console.WriteLine($"Audio engine switched to: {deviceName}");
            LogRoute(route);
            return route;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio engine failed to start: {ex.Message}");
            return FailedToStart(ex);
        }
    });

    public void Stop() => _engine?.Stop();

    private static void LogRoute(MasterRoute route) =>
        Console.WriteLine(route.DeviceName is null
            ? $"[Audio] master silent: {route.Reason}"
            : $"[Audio] master on '{route.DeviceName}': {route.Reason}");

    private MasterRoute FailedToStart(Exception ex) =>
        new(null, $"the audio engine failed to start: {ex.Message}");

    private AudioEngine CreateEngine() =>
        _engineFactory.Create(_engineDependents, _pipeWireRouter, _controllerSoundCard, _deckFormat,
            _decks.Deck1.Engine, _decks.Deck2.Engine);

    private void Attach(AudioEngine engine)
    {
        _engine = engine;
        _masterCue.Attach(engine);
    }
}
