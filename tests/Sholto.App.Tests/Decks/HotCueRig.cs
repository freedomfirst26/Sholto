using Microsoft.Extensions.Options;
using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Processing;
using Sholto.App.Analysis.Reporting;
using Sholto.App.Analysis.Stores;
using Sholto.App.Analysis.Stems;
using Sholto.App.Audio;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.App.Library.Markers;
using Sholto.App.Tests.Segments;
using Sholto.Data;

namespace Sholto.App.Tests;

/// <summary>Two deck sessions whose transport records its seeks (and moves the scripted playhead), a library
/// that has scanned the three rig tracks, and a <see cref="DeckHotCues"/> over a <see cref="FakeHotCueStore"/>.</summary>
internal sealed class HotCueRig
{
    public static readonly Track Alpha = new("/music/a.mp3", "Alpha", "Zed", TimeSpan.FromMinutes(3));
    public static readonly Track Bravo = new("/music/b.mp3", "Bravo", "Abe", TimeSpan.FromMinutes(4));

    private readonly ScriptedPorts[] _ports;

    public HotCueRig(HotCueOptions? options = null)
    {
        var ports1 = new ScriptedPorts(new TestDeckFactory().Create());
        var ports2 = new ScriptedPorts(new TestDeckFactory().Create());
        _ports = [ports1, ports2];
        Transports = [new SeekRecordingTransport(ports1), new SeekRecordingTransport(ports2)];
        Deck1 = new DeckSessionRig(new SeekRecordingPorts(ports1, Transports[0])).Session;
        Deck2 = new DeckSessionRig(new SeekRecordingPorts(ports2, Transports[1])).Session;
        var decks = new DeckPair(Deck1, Deck2);
        var catalog = new LibraryCatalog(Bus);
        var seeds = new LibrarySeeds(catalog);
        var wiring = new LibraryDeckWiring(
            catalog, decks, new Session(), new AnalysisReporter(new[] { AnalysisSteps.Beats }), new ImmediateAppThread());
        Library = new LibrarySession(
            new FakeTrackScanner([Alpha, Bravo]), new NullKeyAnalysisStore(),
            new DemucsStemPresence(_ => Path.Combine(Path.GetTempPath(), "sholto-tests-no-stems")),
            decks, catalog, wiring, seeds, new ImmediateAppThread(), Bus);
        Library.ScanAsync("/music", new LibraryStack(
            new FakeTrackCatalog(), new FakeTagService(new Dictionary<Guid, IReadOnlyList<string>>()),
            new FakeBasicAnalysisStore(new Dictionary<string, double>()), new FakeTempoMultiplierStore())).GetAwaiter().GetResult();
        HotCues = new DeckHotCues(decks, Library, new ImmediateAppThread(), Bus, Markers, Options.Create(options ?? new HotCueOptions()));
    }

    public IDeckSession Deck1 { get; }
    public IDeckSession Deck2 { get; }
    public DataBus Bus { get; } = new(new ThrowingFailureSink());
    public LibrarySession Library { get; }
    public FakeHotCueStore Store { get; } = new();
    public RecordingDeckMarkers Markers { get; } = new();
    public DeckHotCues HotCues { get; }
    public SeekRecordingTransport[] Transports { get; }

    public Guid IdOf(Track track) => Library.TrackIdFor(track.FilePath);

    /// <summary>Loads <paramref name="track"/> on deck 1 with a 120 BPM grid (a beat every 0.5 s) and parks the playhead.</summary>
    public void Load(Track track, double playheadSec = 0)
    {
        Deck1.BeginLoad(track);
        Deck1.LoadTrack(track, track.FilePath, [], bpmMultiplier: 1.0);
        _ports[0].Loading.Analysis.Set(new SyntheticTrackBuilder(120).AddStandardStructure().Build().Analysis);
        Park(playheadSec);
    }

    public void Park(double sec) => _ports[0].ScriptedPlayhead.PositionFrames = (long)(sec * AudioFileDecoder.TargetSampleRate);

    public void SetPlaying(bool playing)
    {
        _ports[0].ScriptedLoading.IsPlaying = playing;
        Deck1.SyncPlayPosition();   // the session follows the transport once a frame
    }
}
