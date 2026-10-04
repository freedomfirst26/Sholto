using Sholto.Data;
using Sholto.App;
using Sholto.App.Decks;
using Sholto.App.Lifecycle;
using Sholto.App.Library;
using Sholto.App.Performance;

namespace Sholto.App;

public sealed class CommandHandlersFactory(IAppThread appThread, IEventPublisher publisher) : ICommandHandlersFactory
{
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
        registry.Register<LoadSelectedIntoDeck>(core.Loader);

        // The library commands and queries (selection, filters, tags, crates) and the on-screen deck tuning.
        var library = new LibraryCommandHandlers(core.Library, _appThread, _publisher);
        registry.Register<SelectTrack>(library);
        registry.Register<FilterLibraryByTag>(library);
        registry.Register<FilterLibraryByCrate>(library);
        registry.Register<ClearLibraryFilter>(library);
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
        registry.Register<ReanalyzeSelected>(core.Loader);

        // The startup questions and their answers, the menu's change requests, and the saved theme.
        registry.Register<ChooseMusicFolder>(lifecycle);
        registry.Register<ChooseOutputDevice>(lifecycle);
        registry.Register<ChangeMusicFolder>(lifecycle);
        registry.Register<ChangeOutputDevice>(lifecycle);
        registry.Register<ChooseTheme>(lifecycle);
    }
}
