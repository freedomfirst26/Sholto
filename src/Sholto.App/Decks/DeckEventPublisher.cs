using Sholto.App.Analysis.Analyzers.Keys;
using Sholto.App.Analysis.Stems;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>Turns one deck session's <see cref="IDeckSession.Changed"/> into the state events an interface
/// follows: content (<see cref="DeckContentChanged"/>), tempo, loop, editing,
/// markers, volume and the per-frame <see cref="DeckFrame"/>. The session already publishes the transport
/// phase, the stem mutes and the echo itself; cue routing publishes the headphone cue. Everything here runs
/// on the app thread (the session raises <c>Changed</c> there), and the per-frame path allocates nothing:
/// a switch on an enum, then a struct handed to the bus.</summary>
public sealed class DeckEventPublisher(IDeckSession session, IEventPublisher publisher)
{
    private readonly IDeckSession _session = session;
    private readonly IEventPublisher _publisher = publisher;

    /// <summary>Publish the full starting picture (so a subscriber that joins later is replayed it) and start
    /// following the session.</summary>
    public void Start()
    {
        PublishContent();
        PublishSections();
        PublishTempo();
        PublishLoop();
        PublishEdit();
        PublishMarkers();
        PublishMix();
        PublishFrame();
        _session.Changed += OnChanged;
    }

    private void OnChanged(DeckChange change)
    {
        switch (change)
        {
            case DeckChange.PlayPosition:
            case DeckChange.Scrubbing:
            case DeckChange.Scratching:
            case DeckChange.MagneticGlow:
                PublishFrame();
                break;
            case DeckChange.LoadedTrack:
            case DeckChange.LoadState:
            case DeckChange.IsLoaded:
            case DeckChange.KeyReady:
            case DeckChange.StemsReady:
            case DeckChange.VocalRegionsReady:
            case DeckChange.Sections:
                PublishContent();
                PublishSections();
                break;
            case DeckChange.AnalysisReset:
                PublishContent();
                PublishTempo();
                break;
            case DeckChange.BasicReady:
                PublishContent();
                PublishTempo();
                break;
            case DeckChange.BpmMultiplier:
            case DeckChange.Tempo:
            case DeckChange.TempoRange:
                PublishTempo();
                PublishFrame();   // the playback speed rides on the frame
                break;
            case DeckChange.MagnetAdjusted:
                PublishTempo();
                break;
            case DeckChange.Loop:
                PublishLoop();
                break;
            case DeckChange.GridNudged:
            case DeckChange.GridEdit:
            case DeckChange.EditOpen:
                PublishEdit();
                break;
            case DeckChange.Markers:
                PublishMarkers();
                break;
            case DeckChange.ChannelGain:
            case DeckChange.Volume:
                PublishMix();
                break;
        }
    }

    private void PublishFrame() =>
        _publisher.Publish(new DeckFrame(
            _session.Index, _session.PlayPosition, _session.PlaybackSeconds, _session.Tempo.PlaybackSpeed,
            _session.IsScrubbing, _session.IsScratching, _session.MagneticGlowSec));

    private void PublishContent() =>
        _publisher.Publish(new DeckContentChanged(
            _session.Index, Project(_session.LoadedTrack), _session.LoadState, _session.IsLoaded,
            Project(_session.Analysis)));

    private DeckTrack? Project(Track? t) =>
        t is null ? null : new DeckTrack(t.FilePath, t.Title, t.Artist, t.Duration);

    /// <summary>A snapshot of what has landed so far; the bag itself keeps filling, the snapshot does not.</summary>
    private DeckAnalysis Project(TrackAnalysis a)
    {
        var basic = a.Basic;
        return new DeckAnalysis(
            basic?.Peaks, basic?.Bpm ?? 0, basic?.BeatTimes ?? [], basic?.DownbeatTimes ?? [],
            a.Get<IReadOnlyList<VocalRegion>>(), a.Has<StemPaths>(), a.Get<KeyAnalysis>()?.Key.ToRef());
    }

    private void PublishSections()
    {
        var grid = _session.SectionGrid;
        var sections = new DeckSection[_session.Sections.Count];
        for (int i = 0; i < sections.Length; i++)
        {
            var s = _session.Sections[i];
            sections[i] = new DeckSection(Enum.Parse<DeckSectionKind>(s.Kind.ToString()), s.StartBar, s.Bars);
        }
        int totalBars = grid.IsEmpty ? 0 : (int)(grid.DurationSec / grid.BarPeriodSec);
        _publisher.Publish(new DeckSectionsChanged(
            _session.Index, sections, new DeckPhraseGrid(_session.PhraseGrid.PhaseBar, _session.PhraseGrid.PhraseBars),
            grid.FirstDownbeatSec, grid.BarPeriodSec, totalBars));
    }

    private void PublishTempo() =>
        _publisher.Publish(new DeckTempoChanged(
            _session.Index, _session.SourceBpm, _session.BpmMultiplier, _session.EffectiveBpm,
            _session.Tempo.PlaybackSpeed, _session.Tempo.TempoRange, _session.IsTempoShifted,
            _session.WasMagnetAdjusted));

    private void PublishLoop()
    {
        if (_session.Looping.ActiveLoop is { } region)
            _publisher.Publish(new DeckLoopChanged(
                _session.Index, true,
                region.StartSample / 2.0 / AudioFileDecoder.TargetSampleRate,
                region.EndSample / 2.0 / AudioFileDecoder.TargetSampleRate));
        else
            _publisher.Publish(new DeckLoopChanged(_session.Index, false, 0, 0));
    }

    private void PublishEdit() =>
        _publisher.Publish(new DeckEditChanged(
            _session.Index, _session.EditOpen, _session.GridEditActive, _session.Beatgrid.IsGridNudged));

    private void PublishMarkers() => _publisher.Publish(new DeckMarkersChanged(_session.Index, _session.MarkerSecs));

    private void PublishMix() =>
        _publisher.Publish(new DeckMixChanged(
            _session.Index, _session.GainKnown, _session.ChannelGain ?? 0, _session.EffectiveGain, _session.IsMuted));
}
