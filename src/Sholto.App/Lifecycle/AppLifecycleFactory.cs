using Sholto.App.Audio;
using Sholto.Data;
using SoundFlow.Structs;

namespace Sholto.App.Lifecycle;

/// <summary>Holds the leaves the lifecycle shares and builds one <see cref="AppLifecycle"/> (and the
/// <see cref="AudioOutput"/> behind it) per call.</summary>
/// <remarks><c>musicDirOverride</c> is the <c>SHOLTO_MUSIC_DIR</c> environment variable, read once by the
/// composition root.</remarks>
public sealed class AppLifecycleFactory(
    ILibraryDatabase database,
    ISettingPreference themePreference,
    ISettingPreference musicDirPreference,
    ISettingPreference outputDevicePreference,
    IAudioOutputEnumerator outputEnumerator,
    IAudioEngineFactory audioEngineFactory,
    IReadOnlyList<INeedsAudioEngine> engineDependents,
    IPipeWireRouter pipeWireRouter,
    AudioFormat deckFormat,
    IMasterCueEngineSink masterCue,
    IAppThread appThread,
    IEventPublisher publisher,
    string? musicDirOverride) : IAppLifecycleFactory
{
    private readonly ILibraryDatabase _database = database;
    private readonly ISettingPreference _themePreference = themePreference;
    private readonly ISettingPreference _musicDirPreference = musicDirPreference;
    private readonly ISettingPreference _outputDevicePreference = outputDevicePreference;
    private readonly IAudioOutputEnumerator _outputEnumerator = outputEnumerator;
    private readonly IAudioEngineFactory _audioEngineFactory = audioEngineFactory;
    private readonly IReadOnlyList<INeedsAudioEngine> _engineDependents = engineDependents;
    private readonly IPipeWireRouter _pipeWireRouter = pipeWireRouter;
    private readonly AudioFormat _deckFormat = deckFormat;
    private readonly IMasterCueEngineSink _masterCue = masterCue;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    private readonly string? _musicDirOverride = musicDirOverride;

    public IAppLifecycle Create(CoreStack core, IControllerSoundCard controllerSoundCard)
    {
        var audio = new AudioOutput(
            _audioEngineFactory, _engineDependents, _pipeWireRouter, controllerSoundCard, _deckFormat,
            core.Decks, _masterCue);
        return new AppLifecycle(
            _database, core.Library, core.Markers, _themePreference, _musicDirPreference, _outputDevicePreference,
            _outputEnumerator, audio, controllerSoundCard, _appThread, _publisher, _musicDirOverride);
    }
}
