namespace Sholto.Bench.Scenario;

/// <summary>
/// One scripted step. Deliberately a flat bag of optional fields rather than a
/// discriminated union — keeps the JSON, and this type, dead simple to write by
/// hand or generate. <see cref="ScenarioParser"/> validates that the fields an
/// action needs are actually present; a field an action doesn't use is ignored.
/// </summary>
public sealed class ScenarioAction
{
    /// <summary>One of: load, gain, play, crossfader, wait, key, click, screenshot,
    /// scan, gesture, midi. See <see cref="ScenarioParser"/> for the field each
    /// requires.</summary>
    public required string Action { get; init; }

    /// <summary>1 or 2 — which deck. Required by load/gain/play. Also used by
    /// gesture (per-deck events) and midi's resulting log line — optional there
    /// since the wire event itself carries no deck (it's the mapping's job to
    /// resolve one), purely descriptive.</summary>
    public int? Deck { get; init; }

    /// <summary>Absolute path to an audio file. Required by load.</summary>
    public string? Track { get; init; }

    /// <summary>load: initial channel gain (0..1, default 1.0). gain: the new
    /// channel gain. crossfader: 0..1, 0 = full deck 1, 1 = full deck 2 (same
    /// equal-power curve as <c>MainViewModel.Crossfader</c>). gesture: the
    /// continuous-control payload (EqMoved/FilterMoved/TempoMoved/CrossfaderMoved/
    /// ChannelVolumeMoved value or position, 0..1 except crossfader).</summary>
    public double? Value { get; init; }

    /// <summary>wait: how long to advance, directly. Mutually exclusive with
    /// Beats/Bpm.</summary>
    public double? Seconds { get; init; }

    /// <summary>wait: how long to advance, in beats. Requires Bpm alongside it —
    /// Bench never runs real beat detection (no analysis pipeline is wired), so
    /// there is no beatgrid to convert against; the scenario must say the tempo
    /// it means.</summary>
    public double? Beats { get; init; }

    /// <summary>wait: the tempo Beats is counted against. See Beats.</summary>
    public double? Bpm { get; init; }

    /// <summary>key: an <see cref="Avalonia.Input.Key"/> name, e.g. "D1", "Space", "M".
    /// ui-only.</summary>
    public string? Key { get; init; }

    /// <summary>click: name of the control to click — currently only "TrackList"
    /// is wired. ui-only.</summary>
    public string? Target { get; init; }

    /// <summary>scan: directory to scan into the library (real file-system scan,
    /// same <c>ITrackScanner</c> the app uses — no fake list). ui-only, since only
    /// the ui host builds a real <c>MusicLibrary</c>.</summary>
    public string? Dir { get; init; }

    /// <summary>click: which row of Target to click (0-based). Defaults to 0.</summary>
    public int? Index { get; init; }

    /// <summary>screenshot: PNG output path. Also usable as a mid-scenario
    /// screenshot step from the ui host, not just the standalone subcommand.</summary>
    public string? Out { get; init; }

    /// <summary>gesture: which <c>ControllerEvent</c> record to build — the record's
    /// type name (e.g. "JogRotated", "PlayPressed", "CueChanged"). See
    /// <see cref="Sholto.Bench.Controller.ScenarioGestureBuilder"/> for the full list
    /// and the fields each one reads. ui-only — needs the real GestureRecognizer/
    /// GestureBus/Orchestrator stack, which only the ui host composes.</summary>
    public string? Event { get; init; }

    /// <summary>gesture (JogRotated/NudgeGrid): signed tick count / beat offset.
    /// gesture (BrowseRotated): signed step count.</summary>
    public int? Delta { get; init; }

    /// <summary>gesture (JogRotated): "top" (TopPlatter) or "ring" (SideRing).</summary>
    public string? JogSource { get; init; }

    /// <summary>gesture: generic boolean payload — JogTouch.Touching,
    /// CueChanged.On, MasterCueChanged.On, DeckShift.Pressed,
    /// StemLevelMode.Pressed.</summary>
    public bool? On { get; init; }

    /// <summary>gesture (StemToggle): 0=Drums, 1=Vocals, 2=Instrumental.</summary>
    public int? Group { get; init; }

    /// <summary>gesture (EqMoved): "Low", "Mid" or "High".</summary>
    public string? Band { get; init; }

    /// <summary>gesture (BeatLoopToggle): loop length in bars.</summary>
    public int? Bars { get; init; }

    /// <summary>gesture (TransportCuePressed): whether the Shift chord fired.</summary>
    public bool? Shifted { get; init; }

    /// <summary>gesture (PadPageSelected): "HotCue" or "PadFx1".</summary>
    public string? Page { get; init; }

    /// <summary>midi: 1-indexed MIDI channel (matches what <c>SHOLTO_MIDI_LOG</c>
    /// prints and what DdjFlx4Mapping expects).</summary>
    public int? MidiChannel { get; init; }

    /// <summary>midi: wire note number for a NoteEvent. Exactly one of Note/Cc
    /// is required.</summary>
    public int? Note { get; init; }

    /// <summary>midi (note): velocity byte. Defaults to 127 for a down-press, 0
    /// for a release (IsDown false).</summary>
    public int? Velocity { get; init; }

    /// <summary>midi (note): true = NoteOn/press, false = NoteOff/release.
    /// Defaults to true.</summary>
    public bool? IsDown { get; init; }

    /// <summary>midi: wire CC number for a CcEvent. Exactly one of Note/Cc is
    /// required.</summary>
    public int? Cc { get; init; }

    /// <summary>midi (cc): the CC's 0..127 value.</summary>
    public int? CcValue { get; init; }
}
