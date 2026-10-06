using Sholto.App.Analysis.Analyzers.Beats;
using Sholto.App.Analysis.Analyzers.Segments;
using Sholto.App.Analysis.Harmony;
using Sholto.App.Audio;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.App.Decks;

/// <summary>One deck's app state and logic, headless: no Avalonia types. It is the deck's
/// <see cref="IDeckPorts"/> plus everything the app decides about it (load state, the CanPlay gate, play
/// phase and end-of-track flash, BPM multiplier, tempo range, fader pickup, stems, cue, echo, markers,
/// segments, grid edit). It publishes <see cref="DeckPlayStateChanged"/>, <see cref="StemMuteChanged"/>
/// and <see cref="EchoChanged"/> on the bus; it raises <see cref="Changed"/> for everything else, which
/// <see cref="DeckEventPublisher"/> republishes as bus events (what the deck view model follows). All
/// members run on the app thread.</summary>
public interface IDeckSession : IDeckPorts
{
    /// <summary>0 for deck 1, 1 for deck 2 — the Deck field of the events it publishes.</summary>
    int Index { get; }

    /// <summary>Raised for every state change; allocation-free.</summary>
    event Action<DeckChange>? Changed;

    /// <summary>Typed mirror of load-state changes.</summary>
    event Action<DeckLoadState>? LoadStateChanged;

    /// <summary>Raised when the user chose a new BPM multiplier (path, multiplier); the owner persists it.
    /// Not raised for a multiplier restored from disk by a load.</summary>
    event Action<string, double>? BpmMultiplierChosen;

    // ---- Load ---------------------------------------------------------------------------------
    Track? LoadedTrack { get; }
    DeckLoadState LoadState { get; }
    bool IsLoaded { get; }
    bool HasAnalysis { get; }
    bool HasStems { get; }

    /// <summary>True when the deck may start playback: loaded AND basic analysis (beat grid) present.</summary>
    bool CanPlay { get; }

    /// <summary>Show the incoming track at once and clear the previous track's derived state.</summary>
    void BeginLoad(Track track, double bpmMultiplier = 1.0);

    void LoadStreaming(Track track, string filePath, double bpmMultiplier = 1.0);

    void LoadTrack(Track track, string filePath, float[] samples, double bpmMultiplier = 1.0);

    void LoadFailed();

    void Unload();

    // ---- Transport ----------------------------------------------------------------------------
    bool IsPlaying { get; }

    /// <summary>Stopped when not playing, Ending past 90 % of the track, else Playing.</summary>
    PlayPhase PlayState { get; }

    /// <summary>The end-of-track flash phase: true while Ending and in the lit half of the flash period.
    /// Derived from the frame clock, so the controller light and the disc ring share it.</summary>
    bool EndFlashOn { get; }

    double PlayPosition { get; set; }

    /// <summary>Current playback time in seconds.</summary>
    double PlaybackSeconds { get; }

    /// <summary>Play, or pause; refused (silently) on the first press until the deck CanPlay.</summary>
    void TogglePlay();

    /// <summary>The per-frame tick: follow the audio deck's transport and position, then advance the
    /// end-of-track flash from the frame clock. Allocates nothing.</summary>
    void SyncPlayPosition();

    // ---- Analysis, markers, segments ----------------------------------------------------------
    TrackAnalysis Analysis { get; }

    /// <summary>Typed key of the loaded track, or null until key analysis lands.</summary>
    Key? LoadedKey { get; }

    double SourceBpm { get; }

    /// <summary>Time of the downbeat nearest the playhead, or -1.</summary>
    double NearestDownbeatSec();

    double[] MarkerSecs { get; }

    void SetMarkers(IReadOnlyList<double> secs);

    /// <summary>Phrase-aware sections of the loaded track; empty until basic analysis lands or when the
    /// track has no usable grid. Recomputed on every <c>BasicReady</c>, so a grid nudge moves them.</summary>
    IReadOnlyList<SongSection> Sections { get; }

    /// <summary>Where phrase lines fall, in bars from the grid's first downbeat.</summary>
    PhraseGrid PhraseGrid { get; }

    /// <summary>The grid the sections were computed on; turns a bar into seconds (<c>DownbeatAt</c>).</summary>
    Beatgrid SectionGrid { get; }

    // ---- BPM, tempo ---------------------------------------------------------------------------
    double BpmMultiplier { get; }

    double EffectiveBpm { get; }

    void ToggleBpmOverride();
    void HalveBpm();
    void DoubleBpm();
    void ResetBpmMultiplier();

    /// <summary>Reset tempo and grid to the analysed detection.</summary>
    void ResetToAnalysis();

    bool IsTempoShifted { get; }

    bool WasMagnetAdjusted { get; }

    /// <summary>Retune this deck's tempo fader so its effective BPM matches <paramref name="targetBpm"/>
    /// (clamped to the fader range). False when there is nothing to do.</summary>
    bool MatchEffectiveBpm(double targetBpm);

    /// <summary>The tempo fader, 0..1, 0.5 = unity. User-driven: clears the magnet-adjusted flag.</summary>
    void SetTempoPosition(double pos);

    void SetTempoRange(double range);

    /// <summary>Cycle the pitch-fader range: 6 % -> 10 % -> 16 % -> WIDE -> 6 %.</summary>
    void CycleTempoRange();

    // ---- Grid, tune editor --------------------------------------------------------------------
    bool GridEditActive { get; }

    void ToggleGridEdit();

    /// <summary>A waveform click while in grid-edit mode: first click is anchor A, second sets the grid.</summary>
    void OnGridClick(double seconds);

    /// <summary>The tune editor is open on this deck (arms the keyboard tempo/grid keys).</summary>
    bool EditOpen { get; }

    void ToggleEdit();
    void OpenEdit();
    void CloseEdit();

    // ---- Stems --------------------------------------------------------------------------------
    bool DrumsActive { get; set; }
    bool VocalsActive { get; set; }
    bool InstrumentalActive { get; set; }
    double DrumsLevel { get; set; }
    double VocalsLevel { get; set; }
    double InstrumentalLevel { get; set; }

    // ---- Performance flags --------------------------------------------------------------------
    bool IsScrubbing { get; set; }
    bool IsScratching { get; set; }
    double MagneticGlowSec { get; set; }

    // ---- Mixer, cue, echo ---------------------------------------------------------------------
    /// <summary>Channel fader 0..1, or null until measured. Unmeasured plays at unity.</summary>
    double? ChannelGain { get; set; }

    bool GainKnown { get; }

    /// <summary>Channel x crossfade gain, 0 when unmeasured.</summary>
    double EffectiveGain { get; }

    /// <summary>Measured and effectively silent.</summary>
    bool IsMuted { get; }

    void SetCrossfadeGain(float gain);

    bool CueActive { get; set; }

    bool EchoActive { get; set; }
}
