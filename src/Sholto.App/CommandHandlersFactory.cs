using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.App;
using Sholto.App.Decks;
using Sholto.App.Glance;
using Sholto.App.Lifecycle;
using Sholto.App.Library;
using Sholto.App.Performance;

namespace Sholto.App;

public sealed class CommandHandlersFactory(IAppThread appThread, IEventPublisher publisher, IHintCounter hintCounter, IOptions<GlanceOptions> glanceOptions) : ICommandHandlersFactory
{
    private readonly IHintCounter _hintCounter = hintCounter;
    private readonly IOptions<GlanceOptions> _glanceOptions = glanceOptions;
    private readonly IAppThread _appThread = appThread;
    private readonly IEventPublisher _publisher = publisher;

    public void Register(ICommandRegistry registry, IQueryRegistry queries, CoreStack core, ICueRouting cueRouting,
        IInspectMode inspectMode, IPlatter platter, IAppLifecycle lifecycle)
    {
        var transport = new TransportCommandHandlers(core.Decks, core.Playback, cueRouting);
        registry.Register<TogglePlay>(transport);
        registry.Register<RestartTrack>(transport);
        registry.Register<ToggleHeadphoneCue>(transport);
        registry.Register<ToggleMasterCue>(transport);
        registry.Register<CycleTempoRange>(transport);

        var mixer = new MixerCommandHandlers(core.Decks, core.Mixer);
        registry.Register<SetCrossfader>(mixer);
        registry.Register<SetChannelVolume>(mixer);
        registry.Register<SetEq>(mixer);
        registry.Register<SetStemLevel>(mixer);
        registry.Register<SetFilter>(mixer);
        registry.Register<SetTempo>(mixer);

        var pads = new PadCommandHandlers(core.Decks, cueRouting);
        registry.Register<ToggleStem>(pads);
        registry.Register<ToggleEcho>(pads);
        registry.Register<HoldRoll>(pads);
        registry.Register<SelectPadPage>(pads);

        var loops = new LoopCommandHandlers(core.Decks, core.Markers);
        registry.Register<ToggleBeatLoop>(loops);
        registry.Register<HalveLoop>(loops);
        registry.Register<DoubleLoop>(loops);
        registry.Register<NudgeGrid>(loops);
        registry.Register<AddMarker>(loops);
        registry.Register<OpenGridEditor>(loops);

        var browse = new BrowseCommandHandlers(core.Library);
        registry.Register<RotateBrowse>(browse);
        registry.Register<SetSearchPick>(core.SearchPick);
        registry.Register<LoadSelectedIntoDeck>(core.Loader);
        registry.Register<UndoLastLoad>(core.Loader);

        // The library commands and queries (selection, tags, crates) and the on-screen deck tuning.
        var membership = new CrateMembershipCache(core.Library);
        var library = new LibraryCommandHandlers(core.Library, _appThread, _publisher, membership);
        registry.Register<SelectTrack>(library);
        registry.Register<AddTagToTrack>(library);
        registry.Register<RemoveTagFromTrack>(library);
        registry.Register<AddTrackToCrate>(library);
        var libraryQueries = new LibraryQueryHandlers(core.Library);
        queries.Register<GetTrackTags, Task<IReadOnlyList<string>>>(libraryQueries);
        queries.Register<SuggestTags, Task<IReadOnlyList<string>>>(libraryQueries);
        queries.Register<SearchTags, Task<IReadOnlyList<TagHit>>>(libraryQueries);
        queries.Register<TopTags, Task<IReadOnlyList<TagHit>>>(libraryQueries);
        queries.Register<TagsByName, Task<IReadOnlyList<TagHit>>>(libraryQueries);
        queries.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(libraryQueries);

        // Glance: rank the whole catalogue, narrowed by the chips, against the other deck, suggest the load target.
        queries.Register<RankTracks, Task<RankedTracks>>(
            new RankTracksHandler(core.Library, core.Decks, new GlanceRanker(new FitScorer(_glanceOptions), new GlanceQueryFactory(), new GlanceMatcher()),
                new GlanceScope(), membership, _glanceOptions));
        queries.Register<SuggestLoadTarget, int>(new SuggestLoadTargetHandler(core.Decks));
        registry.Register<LoadSongToTrackList>(core.TrackList);
        registry.Register<LoadCrateToTrackList>(core.TrackList);
        registry.Register<LoadTagToTrackList>(core.TrackList);
        registry.Register<RemoveFromTrackList>(core.TrackList);
        registry.Register<RemoveSourceFromTrackList>(core.TrackList);
        registry.Register<MoveInTrackList>(core.TrackList);
        registry.Register<ClearTrackList>(core.TrackList);

        var tuning = new DeckTuningCommandHandlers(core.Decks);
        registry.Register<ChangeBpmMultiplier>(tuning);
        registry.Register<ResetDeckToAnalysis>(tuning);
        registry.Register<AdjustBpm>(tuning);
        registry.Register<NudgeGridFine>(tuning);
        registry.Register<ToggleTuneEditor>(tuning);
        registry.Register<CloseTuneEditor>(tuning);
        registry.Register<ClickGrid>(tuning);

        registry.Register<ReportDeviceConnection>(new DeviceConnectionHandler(_publisher));

        // Inspect itself, and the controls that only exist to be echoed while Inspect is on.
        registry.Register<SetInspectMode>(inspectMode);
        registry.Register<ReportControl>(new ReportControlHandler());

        registry.Register<TouchPlatter>(platter);
        registry.Register<TurnPlatter>(platter);
        // How long a flung platter coasts after release (Settings ▸ Backspin release); the lifecycle saves it.
        registry.Register<SetBackspinTime>(core.BackspinFeel);
        registry.Register<SetBackspinDistance>(core.BackspinFeel);
        registry.Register<ReanalyzeSelected>(core.Loader);

        // The startup questions and their answers, the menu's change requests, the saved theme and waveform style.
        registry.Register<ChooseMusicFolder>(lifecycle);
        registry.Register<ChooseOutputDevice>(lifecycle);
        registry.Register<ChangeMusicFolder>(lifecycle);
        registry.Register<ChangeOutputDevice>(lifecycle);
        registry.Register<ChooseTheme>(lifecycle);
        registry.Register<ChooseWaveformStyle>(lifecycle);

        // Each one-time hint shown is saved across launches. The count query is answered earlier, by the composition root.
        registry.Register<RecordHintShown>(_hintCounter);
    }
}
