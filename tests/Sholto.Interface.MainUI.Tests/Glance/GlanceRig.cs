using Microsoft.Extensions.Options;
using Sholto.Data;
using Sholto.Interface.MainUI.Models;
using Sholto.Interface.MainUI.Theming;
using Sholto.Interface.MainUI.ViewModels;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.Tests.Glance;

/// <summary>A Glance view model on a real bus with fake App handlers: the ranking is answered by a function
/// the test sets (default: every visible row, fitted to the deck that is not the target), commands are
/// logged in order, and the clock moves only when the test says so.</summary>
internal sealed class GlanceRig
{
    public DataBus Bus { get; } = new(new ThrowingFailureSink());
    public FakeFrameClock Clock { get; } = new();
    public FakeDeckClockSource Decks { get; } = new();
    public List<object> Log { get; } = [];
    public LibraryRowsViewModel Library { get; }
    public GlanceHeaderViewModel Header { get; }
    public GlanceViewModel Glance { get; }
    public F9QueryHandler<RankTracks, Task<RankedTracks>> Ranker { get; }
    public int SuggestedTarget { get; set; }
    public List<CrateRef> Crates { get; } = [];
    public List<TagHit> Tags { get; } = [];

    /// <summary>What the fake ranker returns for a query; default ranks the visible rows in order.</summary>
    public Func<RankTracks, Task<RankedTracks>> Answer { get; set; }

    public GlanceRig(bool reducedMotion = false)
    {
        AvaloniaTestApp.EnsureStarted();
        var theme = new ThemeStackFactory().Build().Context;
        Library = new LibraryRowsViewModel(Bus, new TrackRowFactory(theme));
        Answer = q => Task.FromResult(Ranked(q.TargetDeck, Library.Items.Select(r => Summary(r)).ToList()));
        Ranker = new F9QueryHandler<RankTracks, Task<RankedTracks>>(q => Answer(q));
        Bus.Register<RankTracks, Task<RankedTracks>>(Ranker);
        Bus.Register<SuggestLoadTarget, int>(new F9QueryHandler<SuggestLoadTarget, int>(_ => SuggestedTarget));
        Bus.Register<SearchCrates, Task<IReadOnlyList<CrateRef>>>(
            new F9QueryHandler<SearchCrates, Task<IReadOnlyList<CrateRef>>>(_ => Task.FromResult<IReadOnlyList<CrateRef>>([.. Crates])));
        Bus.Register<SearchTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<SearchTags, Task<IReadOnlyList<TagHit>>>(_ => Task.FromResult<IReadOnlyList<TagHit>>([.. Tags])));
        Bus.Register<TopTags, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TopTags, Task<IReadOnlyList<TagHit>>>(_ => Task.FromResult<IReadOnlyList<TagHit>>([.. Tags])));
        Bus.Register<TagsByName, Task<IReadOnlyList<TagHit>>>(
            new F9QueryHandler<TagsByName, Task<IReadOnlyList<TagHit>>>(_ => Task.FromResult<IReadOnlyList<TagHit>>([])));
        Bus.Register(new LoggingCommandHandler<SetSearchPick>(Log));
        Bus.Register(new LoggingCommandHandler<LoadSelectedIntoDeck>(Log));
        Bus.Register(new LoggingCommandHandler<RemoveFromTrackList>(Log));
        Bus.Register(new LoggingCommandHandler<LoadSongToTrackList>(Log));
        Bus.Register(new LoggingCommandHandler<LoadCrateToTrackList>(Log));
        Bus.Register(new LoggingCommandHandler<LoadTagToTrackList>(Log));
        Bus.Register(new LoggingCommandHandler<ClearTrackList>(Log));
        Bus.Register(new LoggingCommandHandler<UndoLastLoad>(Log));
        Header = new GlanceHeaderViewModel(Decks, Clock, Bus, new DeckSlotFactory(Decks, new FixedMotionPreference(reducedMotion), Options.Create(new GlanceViewOptions())));
        Glance = new GlanceViewModel(
            Bus, Bus, Bus, new ImmediateAppThread(), Clock, Library, new TagRecency(), Header,
            new FixedMotionPreference(reducedMotion), Options.Create(new GlanceViewOptions()));
    }

    public IEnumerable<SetSearchPick> Picks => Log.OfType<SetSearchPick>();

    public static TrackSummary Track(string title, string artist = "Artist", double bpm = 128) =>
        new($"/music/{title}.mp3", title, artist, TimeSpan.FromMinutes(3))
        {
            TrackId = Guid.NewGuid(),
            Bpm = bpm,
            MusicalKey = new KeyRef(9, false),
        };

    /// <summary>Show these tracks as the visible library rows.</summary>
    public void Show(params TrackSummary[] tracks) =>
        Bus.Publish(new LibraryRowsChanged(tracks, 1));

    /// <summary>These tracks are the Track List (the list itself, not the rows the main view shows).</summary>
    public void PutInList(params TrackSummary[] tracks) =>
        Bus.Publish(new TrackListChanged(
            [new TrackListSource("songs", TrackListSourceKind.Songs, "Songs", tracks.Length)], tracks.Length)
        { Paths = tracks.Select(t => t.FilePath).ToList() });

    public static TrackSummary Summary(TrackRow row) => new(row.FilePath, row.Title, row.Artist, row.Duration);

    /// <summary>A ranking of <paramref name="rows"/> fitted to the deck that is not <paramref name="target"/>.</summary>
    public static RankedTracks Ranked(int target, IReadOnlyList<TrackSummary> rows, bool fitActive = true) =>
        new(rows.Select(s => new RankedTrack(s, FitLevel.Good, 0.8, false)).ToList(),
            fitActive ? 1 - target : -1,
            fitActive ? new KeyRef(9, false) : null,
            fitActive ? 130.0 : null,
            fitActive,
            []);

    /// <summary>The library database is up, so the rail offers crates and tags.</summary>
    public void AttachDatabase() => Bus.Publish(new LibraryDatabaseAttached(true));

    /// <summary>One frame: the re-rank tick runs.</summary>
    public void Tick()
    {
        Clock.Advance(0.016);
        Glance.OnFrame(Clock.Now);
        Header.OnFrame(Clock.Now);
    }
}
