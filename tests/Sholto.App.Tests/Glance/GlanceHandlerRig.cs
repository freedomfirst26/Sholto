using Sholto.App.Analysis.Analyzers;
using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Analyzers.Waveform;
using Sholto.App.Decks;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Tests.Glance;

/// <summary>A scanned library (<see cref="LibrarySessionRig"/>) plus a separate pair of scripted decks the Glance
/// handlers read: a test loads a track into a deck, gives it a key and BPM, and says whether and where it plays.</summary>
internal sealed class GlanceHandlerRig
{
    private readonly GlanceTrackBuilder _keys = new();
    private readonly ScriptedPorts[] _ports = [new(new TestDeckFactory().Create()), new(new TestDeckFactory().Create())];
    private readonly IDeckSession[] _sessions;

    public GlanceHandlerRig(params Track[] tracks)
    {
        Library = new LibrarySessionRig(tracks);
        _sessions = [new DeckSessionRig(_ports[0]).Session, new DeckSessionRig(_ports[1]).Session];
        Decks = new DeckPair(_sessions[0], _sessions[1]);
        // On the pool, so the scan does not capture the test framework's synchronization context.
        Task.Run(() => Library.Library.ScanAsync("/music", null)).GetAwaiter().GetResult();
    }

    public LibrarySessionRig Library { get; }
    public IDecks Decks { get; }

    /// <summary>Store keys and raw BPMs for catalog files, so the rows carry them.</summary>
    public void Seed(IReadOnlyDictionary<string, (string Key, double Bpm)> facts)
    {
        Library.Library.SeedKnownBpms(facts.ToDictionary(f => f.Key, f => f.Value.Bpm));
        Library.Library.SeedKnownKeys(facts.ToDictionary(f => f.Key, f => _keys.Key(f.Value.Key).ToKey()));
    }

    /// <summary>Load <paramref name="track"/> into a deck. <paramref name="sourceBpm"/> and <paramref name="camelot"/>
    /// are what its analysis found; <paramref name="speed"/> is the tempo-fader playback speed (1 = unity; beyond +-6 % the fader range is widened to 50 %).</summary>
    public void Load(
        int deck, Track track, double? sourceBpm = null, string? camelot = null, bool playing = false,
        double positionSec = 0, double speed = 1.0)
    {
        var session = _sessions[deck];
        session.LoadStreaming(track, track.FilePath);
        if (sourceBpm is { } bpm)
            session.Analysis.Set(new BasicAnalysis(
                new WaveformPeaks(Min: [-0.5f], Max: [0.5f], Low: [0.1f], Mid: [0.2f], High: [0.3f], SamplesPerPeak: 1024, SampleRate: 48000),
                Bpm: bpm, BeatTimes: [], DownbeatTimes: []));
        if (camelot is not null)
            session.Analysis.Set(new KeyAnalysis(_keys.Key(camelot).ToKey()));
        if (Math.Abs(speed - 1.0) > 0.06) session.SetTempoRange(0.5);
        session.SetTempoPosition(0.5 + (speed - 1.0) / (2 * session.Tempo.TempoRange));
        _ports[deck].ScriptedPlayhead.PositionFrames = (long)(positionSec * 48000);
        _ports[deck].ScriptedLoading.IsPlaying = playing;
        session.SyncPlayPosition();
    }
}
