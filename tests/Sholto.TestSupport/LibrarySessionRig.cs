using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stems;
using Sholto.App.Analysis.Stores;
using Sholto.App.Library;
using Sholto.Data;
using Sholto.App.Decks;

namespace Sholto.TestSupport;

/// <summary>A <see cref="LibrarySession"/> over two real deck sessions (scripted ports, no audio), a
/// scanner that finds <see cref="Tracks"/>, and fakes for every store a scan reads. The app thread is
/// immediate, so posted updates land at once.</summary>
internal sealed class LibrarySessionRig
{
    public static readonly Track Alpha = new("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3));
    public static readonly Track Bravo = new("/music/b.mp3", "Bravo", "Abe", TimeSpan.FromMinutes(4));
    public static readonly Track Charlie = new("/music/c.mp3", "Charlie", "Abe", TimeSpan.FromMinutes(5));

    public LibrarySessionRig(params Track[] tracks) : this(new ImmediateAppThread(), null, tracks)
    {
    }

    public LibrarySessionRig(IAppThread appThread, params Track[] tracks) : this(appThread, null, tracks)
    {
    }

    /// <param name="deck1Provider">Replaces deck 1's analysis provider (the one a forced re-analysis uses), or null.</param>
    public LibrarySessionRig(IAppThread appThread, IAnalysisProvider? deck1Provider, params Track[] tracks)
    {
        Tracks = tracks.Length > 0 ? tracks : [Alpha, Bravo, Charlie];
        Deck1 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create(), deck1Provider)).Session;
        Deck2 = new DeckSessionRig(new ScriptedPorts(new TestDeckFactory().Create())).Session;
        Reporter = new AnalysisReporter(new[] { AnalysisSteps.Beats });
        Bus = new DataBus(new ThrowingFailureSink());
        Library = new LibrarySession(
            new FakeTrackScanner(Tracks),
            new NullKeyAnalysisStore(),
            new DemucsStemPresence(_ => Path.Combine(Path.GetTempPath(), "sholto-tests-no-stems")),
            Reporter,
            new DeckPair(Deck1, Deck2),
            new Session(),
            appThread,
            Bus);
    }

    public Track[] Tracks { get; }
    public IDeckSession Deck1 { get; }
    public IDeckSession Deck2 { get; }
    public AnalysisReporter Reporter { get; }
    public DataBus Bus { get; }
    public LibrarySession Library { get; }

    public FakeTrackCatalog Catalog { get; } = new();
    public FakeCrateService Crates { get; } = new();
    public FakeTempoMultiplierStore Multipliers { get; } = new();

    /// <summary>The stores a scan reads and writes, over the given stored facts.</summary>
    public LibraryStack Stack(
        IReadOnlyDictionary<string, double>? bpms = null,
        FakeTagService? tags = null,
        FakeTempoMultiplierStore? multipliers = null) =>
        new(Catalog,
            tags ?? new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>()),
            new FakeBasicAnalysisStore(bpms ?? new Dictionary<string, double>()),
            multipliers ?? Multipliers);

    /// <summary>The row for a track, as the session now holds it.</summary>
    public TrackSummary Row(Track track) => Library.Rows.Single(r => r.FilePath == track.FilePath);
}
