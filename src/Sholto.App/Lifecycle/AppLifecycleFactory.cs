using Sholto.App.Audio;
using Sholto.App.Analysis.Stems;
using Sholto.App.ExternalTools;
using Sholto.App.Lifecycle.Folders;
using Sholto.Data;
using SoundFlow.Structs;

namespace Sholto.App.Lifecycle;

/// <summary>Holds the leaves the lifecycle shares and builds one <see cref="AppLifecycle"/> (and the
/// <see cref="AudioOutput"/> behind it) per call.</summary>
/// <remarks><c>musicOverride</c> and <c>analysisOverride</c> are the <c>SHOLTO_MUSIC_DIR</c> and
/// <c>SHOLTO_CACHE_ROOT</c> environment variables, read once by the composition root.
/// <c>legacyAnalysisRoot</c> is where stems lived before the analysis folder could be chosen.
/// The <see cref="LibraryFolders"/> is built here because it needs the session in the <see cref="CoreStack"/>.</remarks>
public sealed class AppLifecycleFactory(
    ILibraryDatabase database,
    ISettingPreference themePreference,
    ISettingPreference musicDirPreference,
    ISettingPreference analysisDirPreference,
    IAnalysisFolder analysisFolder,
    IStemGate stemGate,
    IStemFolderMover stemMover,
    FolderCheck folderCheck,
    ISettingPreference outputDevicePreference,
    ISettingPreference waveformStylePreference,
    ISettingPreference backspinTimePreference,
    ISettingPreference backspinDistancePreference,
    ISettingPreference legacyShortlistPreference,
    ISettingPreference trackListPreference,
    ISavedTrackListCodec trackListCodec,
    IAudioOutputEnumerator outputEnumerator,
    IAudioEngineFactory audioEngineFactory,
    IReadOnlyList<INeedsAudioEngine> engineDependents,
    IPipeWireRouter pipeWireRouter,
    AudioFormat deckFormat,
    IMasterCueEngineSink masterCue,
    SystemCheck systemCheck,
    IAppThread appThread,
    IEventPublisher publisher,
    string? musicOverride,
    string? analysisOverride,
    string legacyAnalysisRoot) : IAppLifecycleFactory
{
    private readonly ILibraryDatabase _database = database;
    private readonly ISettingPreference _themePreference = themePreference;
    private readonly ISettingPreference _musicDirPreference = musicDirPreference;
    private readonly ISettingPreference _analysisDirPreference = analysisDirPreference;
    private readonly IAnalysisFolder _analysisFolder = analysisFolder;
    private readonly IStemGate _stemGate = stemGate;
    private readonly IStemFolderMover _stemMover = stemMover;
    private readonly FolderCheck _folderCheck = folderCheck;
    private readonly ISettingPreference _outputDevicePreference = outputDevicePreference;
    private readonly ISettingPreference _waveformStylePreference = waveformStylePreference;
    private readonly ISettingPreference _backspinTimePreference = backspinTimePreference;
    private readonly ISettingPreference _backspinDistancePreference = backspinDistancePreference;
    private readonly ISettingPreference _legacyShortlistPreference = legacyShortlistPreference;
    private readonly ISettingPreference _trackListPreference = trackListPreference;
    private readonly ISavedTrackListCodec _trackListCodec = trackListCodec;
    private readonly IAudioOutputEnumerator _outputEnumerator = outputEnumerator;
    private readonly IAudioEngineFactory _audioEngineFactory = audioEngineFactory;
    private readonly IReadOnlyList<INeedsAudioEngine> _engineDependents = engineDependents;
    private readonly IPipeWireRouter _pipeWireRouter = pipeWireRouter;
    private readonly AudioFormat _deckFormat = deckFormat;
    private readonly IMasterCueEngineSink _masterCue = masterCue;
    private readonly SystemCheck _systemCheck = systemCheck;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;
    private readonly string? _musicOverride = musicOverride;
    private readonly string? _analysisOverride = analysisOverride;
    private readonly string _legacyAnalysisRoot = legacyAnalysisRoot;

    public IAppLifecycle Create(CoreStack core, IControllerSoundCard controllerSoundCard)
    {
        var audio = new AudioOutput(
            _audioEngineFactory, _engineDependents, _pipeWireRouter, controllerSoundCard, _deckFormat,
            core.Decks, _masterCue);
        var folders = new LibraryFolders(
            _musicDirPreference, _analysisDirPreference, _analysisFolder, _stemGate, _stemMover, _folderCheck,
            core.Library, _database, _appThread, _publisher, _musicOverride, _analysisOverride, _legacyAnalysisRoot);
        return new AppLifecycle(
            _database, core.Library, core.Markers, _themePreference, folders, _outputDevicePreference,
            _waveformStylePreference, _backspinTimePreference, _backspinDistancePreference, core.BackspinFeel, _legacyShortlistPreference,
            _trackListPreference, core.TrackList, _trackListCodec,
            _outputEnumerator, audio, controllerSoundCard, _systemCheck, _appThread, _publisher);
    }
}
