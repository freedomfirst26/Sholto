using Sholto.App.Audio;
using Sholto.Data;
using DeckContent = Sholto.Data.DeckContentChanged<
    Sholto.App.Library.Track, Sholto.App.Audio.TrackAnalysis, Sholto.App.Analysis.Analyzers.Segments.SongSegment>;

namespace Sholto.App.Decks;

/// <summary>Turns one deck session's <see cref="IDeckSession.Changed"/> into the state events an interface
/// follows: content (<see cref="DeckContentChanged{TTrack, TAnalysis, TSegment}"/>), tempo, loop, editing,
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
            case DeckChange.Segments:
                PublishContent();
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
        _publisher.Publish(new DeckContent(
            _session.Index, _session.LoadedTrack, _session.LoadState, _session.IsLoaded, _session.Analysis, _session.Segments));

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
