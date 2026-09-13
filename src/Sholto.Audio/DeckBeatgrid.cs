using Sholto.Analysis;
using Sholto.Analysis.Analyzers;

namespace Sholto.Audio;

/// <summary>
/// Beatgrid adjustment model, extracted out of <see cref="Deck"/>.
///
/// madmom's detection (the <see cref="BasicAnalysis"/> returned by the analysis
/// provider) is treated as IMMUTABLE — kept in <c>_detectedBasic</c> and never
/// overwritten. Alongside it this class holds exactly two <see cref="Beatgrid"/>
/// values and nothing else:
///     _detected  — the detection reduced to (anchor, beat period, beats/bar)
///     _effective = _detected.AtBpm(override).ShiftedBy(offset) at the live duration
/// and the grid the user sees is _effective materialised. The detected grid's
/// tempo and phase are themselves a least-squares fit
/// through ALL of madmom's raw beats (<see cref="BeatgridFitter.FitGrid"/>), not just
/// the reported Bpm + DownbeatTimes[0] — see <see cref="SetDetectedBasic"/>.
/// That fit falls back to (reported Bpm, first downbeat) when the fit can't be
/// trusted. The pair (_bpmOverride, _offsetSec) is the entire manual
/// correction — tiny, nullable, persisted per-track via
/// <see cref="GridAdjustmentPut"/>, and reloaded on the next load. Null
/// override + zero offset = pristine detection. This is the Rekordbox/Serato
/// model (store an anchor + BPM, not a mutated array) and guarantees beats and
/// downbeats can never drift out of sync the way piecemeal array edits could.
///
/// Deck owns the sample count, current file path and live <see cref="TrackAnalysis"/>
/// (all three are also used by Deck's other components / playback), so they're
/// handed in as accessor delegates rather than via a back-reference to Deck.
/// <see cref="_raiseAnalysisUpdated"/> is the one exception to "no reaching back
/// into Deck" — regenerating the grid needs to fire Deck's own
/// <c>AnalysisUpdated</c> event (which many unrelated Deck operations also
/// fire), so Deck hands in the single delegate that raises it rather than this
/// class exposing its own parallel event Deck would just forward.
/// <see cref="_shiftLoop"/> is a narrow delegate onto <see cref="DeckLooping.ShiftLoop"/>
/// — grid nudges move an in-flight loop by the same delta so it doesn't
/// desync from the beat it was set on.
/// </summary>
internal sealed class DeckBeatgrid : IDeckBeatgrid
{
    private readonly Func<long> _sampleCount;
    private readonly Func<string?> _currentFilePath;
    private readonly Func<TrackAnalysis> _analysis;
    private readonly Action _raiseAnalysisUpdated;
    private readonly Action<double> _shiftLoop;
    // Persists the manual (bpmOverride, offsetSec) correction per track — a
    // constructor parameter (not a settable property) so a DeckBeatgrid can
    // never exist without one. Never null: the composition root hands in
    // NullGridAdjustmentStore / SwitchableGridAdjustmentStore while the DB
    // isn't open yet, so this class never has to ask "is there a cache".
    private readonly IGridAdjustmentStore _gridCache;
    // Constant-spacing grid synthesis / least-squares fit — constructor-injected
    // (same reasoning as IWaveformPeakAnalyzer) so a test/Bench harness can
    // substitute a fake instead of the real math.
    private readonly IBeatgridFitter _fitter;

    public DeckBeatgrid(
        Func<long> sampleCount,
        Func<string?> currentFilePath,
        Func<TrackAnalysis> analysis,
        Action raiseAnalysisUpdated,
        Action<double> shiftLoop,
        IGridAdjustmentStore gridCache,
        IBeatgridFitter fitter)
    {
        _sampleCount = sampleCount;
        _currentFilePath = currentFilePath;
        _analysis = analysis;
        _raiseAnalysisUpdated = raiseAnalysisUpdated;
        _shiftLoop = shiftLoop;
        _gridCache = gridCache;
        _fitter = fitter;
    }

    /// <inheritdoc/>
    public bool IsGridNudged { get; private set; }
    /// <inheritdoc/>
    public event Action<bool>? GridNudgedChanged;
    private void SetGridNudged(bool v)
    {
        if (IsGridNudged == v) return;
        IsGridNudged = v;
        GridNudgedChanged?.Invoke(v);
    }

    private BasicAnalysis? _detectedBasic;  // immutable madmom detection (peaks + version-3 arrays)

    /// <summary>The detected grid: madmom's detection reduced to its four
    /// numbers, with tempo and phase taken from the least-squares fit where the
    /// fit could be trusted. Immutable baseline — the user's corrections never
    /// write here, they produce <see cref="_effective"/>.
    /// Replaces what used to be three loose fields (anchor, beats-per-bar, and a
    /// nullable fitted BPM that every reader had to spell
    /// <c>_detectedFitBpm ?? detected.Bpm</c>).</summary>
    private Beatgrid _detected = Beatgrid.Empty;

    /// <summary>The grid the user actually sees: <see cref="_detected"/> with
    /// the current correction applied, at the current track duration. Recomputed
    /// by <see cref="RegenerateGrid"/>, which is the only writer.</summary>
    private Beatgrid _effective = Beatgrid.Empty;

    // The manual correction itself. Kept as the (nullable override, offset) pair
    // rather than folded into _effective because that pair is what is PERSISTED
    // per track, and because null-override is meaningful: it means "follow the
    // detection", so re-detecting or resetting restores it exactly.
    private double? _bpmOverride;            // null = use the detected (fitted) BPM
    private double _offsetSec;               // phase shift applied to the whole grid

    /// <summary>Effective BPM: explicit override if any, else the detected
    /// (fitted) tempo.</summary>
    private double EffectiveBpm => _bpmOverride ?? _detected.Bpm;

    /// <summary>Clear detection + adjustment for a fresh track. Called from
    /// <c>Deck.BeginLoad</c>. Not on <see cref="IDeckBeatgrid"/> — only Deck
    /// itself calls this, via its concrete field.</summary>
    internal void ResetForNewTrack()
    {
        _detectedBasic = null;
        _detected = Beatgrid.Empty;
        _effective = Beatgrid.Empty;
        _bpmOverride = null;
        _offsetSec = 0.0;
        SetGridNudged(false);
    }

    /// <summary>Store madmom's freshly-detected analysis as the immutable
    /// baseline, fetch any saved per-track correction, and publish the grid
    /// with that correction applied. Called from Deck's analysis-landed paths
    /// instead of <c>Analysis.Set(basic)</c> directly, so a track that was
    /// hand-tuned in a previous session shows the tuned grid the moment
    /// detection lands. Not on <see cref="IDeckBeatgrid"/> — only Deck's
    /// background analysis callbacks call this, via its concrete field.
    /// async void: invoked fire-and-forget from the background analysis task,
    /// same threading contract as the rest of the analysis pipeline.</summary>
    internal async void SetDetectedBasic(BasicAnalysis basic)
    {
        _detectedBasic = basic;

        // Recover the four numbers from the analysis arrays. Beats-per-bar is
        // the only one that is genuinely lost across the analysis cache (which
        // stores arrays, version 3), so Beatgrid.FromGridArrays reads it back
        // from their spacing — once, here at the boundary. Tempo and phase are
        // about to be replaced by the fit below. Duration is filled in per
        // regeneration from the live sample count.
        _detected = Beatgrid.FromGridArrays(basic.BeatTimes, basic.DownbeatTimes, basic.Bpm, 0.0);

        // Fit a constant-spacing grid through ALL of madmom's raw beats rather
        // than trusting just the reported BPM + first downbeat — a reported
        // BPM off by a few hundredths drifts a rigid grid visibly off the
        // kicks by the end of a track. Falls back to (reported BPM, first
        // downbeat) when there's too little data or the track isn't constant
        // tempo. This fit result — not detected.Bpm — becomes the default;
        // detected.Bpm in _detectedBasic is never touched.
        double firstDownbeat = basic.DownbeatTimes.Length > 0 ? basic.DownbeatTimes[0] : 0.0;
        if (basic.DownbeatTimes.Length > 0)
        {
            var fit = _fitter.FitGrid(basic.BeatTimes, basic.Bpm, firstDownbeat);
            Console.WriteLine($"[Deck] grid fit: {(fit.UsedFit ? "used" : "fell back")} — {fit.Reason}");
            // On fallback the grid keeps the tempo and phase FromGridArrays
            // already gave it: the reported BPM and the first detected downbeat.
            if (fit.UsedFit)
                _detected = _detected.AtBpm(fit.Bpm).PivotedAt(fit.AnchorSec);
        }
        else
        {
            Console.WriteLine("[Deck] grid fit: fell back — no detected downbeats");
            _detected = _detected.PivotedAt(firstDownbeat);
        }

        // Publish the pristine detection immediately so the grid appears with
        // no perceptible delay, then apply any saved correction once the
        // (fast, local) DB read returns. Capture the path so a track swap
        // mid-fetch can't apply the wrong track's adjustment.
        string? path = _currentFilePath();
        RegenerateGrid();

        if (path is not null)
        {
            try
            {
                var adj = await _gridCache.TryGetAsync(path);
                // Bail if the user swapped tracks while we were reading.
                if (adj is not null && path == _currentFilePath())
                    ApplyGridAdjustment(adj.Value.BpmOverride, adj.Value.OffsetSec);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Deck] grid adjustment load failed: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public void ApplyGridAdjustment(double? bpmOverride, double offsetSec)
    {
        _bpmOverride = bpmOverride;
        _offsetSec = offsetSec;
        bool adjusted = bpmOverride is not null || Math.Abs(offsetSec) > 1e-9;
        SetGridNudged(adjusted);
        RegenerateGrid();
    }

    /// <summary>Rebuild the effective grid from the detected baseline plus the
    /// current (override, offset), publish it as a derived BasicAnalysis, and
    /// move any active loop to track the offset delta. No-op until detection
    /// lands.
    ///
    /// Duration is re-read every time rather than stored on the detected grid:
    /// detection can land before the exact sample count is known.</summary>
    private void RegenerateGrid()
    {
        var detected = _detectedBasic;
        if (detected is null || detected.Bpm <= 0) return;

        double durationSec = _sampleCount() / (double)AudioFileDecoder.TargetSampleRate;
        if (durationSec <= 0) durationSec = detected.BeatTimes.Length > 0 ? detected.BeatTimes[^1] + 1 : 1;

        // No override → keep the detected period untouched rather than
        // round-tripping it through BPM and back.
        var baseline = _bpmOverride is { } bpm ? _detected.AtBpm(bpm) : _detected;
        _effective = (baseline with { DurationSec = durationSec }).ShiftedBy(_offsetSec);

        var (beats, downs) = _effective.Materialise();
        if (downs.Length == 0) return;

        // Derived analysis keeps the detected waveform peaks; only BPM + grid
        // arrays change. Bpm reflects the effective (possibly overridden) value
        // so loop-length math and the BPM display follow the correction.
        _analysis().Set(detected with
        {
            Bpm = EffectiveBpm, BeatTimes = beats, DownbeatTimes = downs
        });
        _raiseAnalysisUpdated();
    }

    /// <inheritdoc/>
    public void AdjustBpm(double deltaBpm)
    {
        var detected = _detectedBasic;
        if (detected is null || detected.Bpm <= 0) return;
        double detectedBpm = _detected.Bpm;
        double current = EffectiveBpm;
        double next = Math.Clamp(current + deltaBpm, 20.0, 400.0);
        // Snapping back to exactly the detected (fitted) BPM clears the override (null).
        _bpmOverride = Math.Abs(next - detectedBpm) < 1e-6 ? null : next;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] BPM {(_bpmOverride is null ? "→ detected" : $"override → {_bpmOverride:F2}")} (Δ{deltaBpm:+0.00;-0.00})");
    }

    /// <inheritdoc/>
    public void NudgeGrid(int beats)
    {
        var detected = _detectedBasic;
        if (detected is null) return;
        double bpm = EffectiveBpm;
        if (bpm <= 0) return;
        double delta = beats * 60.0 / bpm;
        _shiftLoop(delta);
        _offsetSec += delta;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] grid offset {beats:+#;-#;0} beat ({_offsetSec*1000:+0.0;-0.0} ms total)");
    }

    /// <inheritdoc/>
    public void NudgeGridFine(double seconds)
    {
        if (_detectedBasic is null || seconds == 0.0) return;
        _shiftLoop(seconds);
        _offsetSec += seconds;
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] grid offset fine ({_offsetSec*1000:+0.0;-0.0} ms total)");
    }

    /// <inheritdoc/>
    public void SetGridFromTwoPoints(double tA, double tB)
    {
        var detected = _detectedBasic;
        if (detected is null) return;
        if (tB < tA) (tA, tB) = (tB, tA);
        double span = tB - tA;
        if (span < 0.5) { Console.WriteLine("[Deck] two-point: points too close — ignored"); return; }

        double detectedBpm = _detected.Bpm;
        double curBpm = EffectiveBpm;
        if (curBpm <= 0) return;
        int beatsPerBar = _detected.BeatsPerBar;
        double barPeriodCur = (60.0 / curBpm) * beatsPerBar;
        int bars = Math.Max(1, (int)Math.Round(span / barPeriodCur));

        double newBeatPeriod = (span / bars) / beatsPerBar;
        double newBpm = Math.Clamp(60.0 / newBeatPeriod, 20.0, 400.0);

        _bpmOverride = Math.Abs(newBpm - detectedBpm) < 1e-6 ? null : newBpm;
        _offsetSec = tA - _detected.AnchorSec;   // make tA a downbeat
        RegenerateGrid();
        AfterAdjustment();
        Console.WriteLine($"[Deck] two-point grid: {bars} bar(s) in {span:F3}s → {newBpm:F3} BPM, anchor {tA:F3}s");
    }

    /// <inheritdoc/>
    public void ResetGrid()
    {
        _bpmOverride = null;
        _offsetSec = 0.0;
        RegenerateGrid();
        SetGridNudged(false);
        string? path = _currentFilePath();
        if (path is not null)
            _ = _gridCache.PutAsync(path, null, 0.0);
        Console.WriteLine("[Deck] grid reset to detected");
    }

    /// <summary>Mark nudged + persist the (override, offset) pair. Clears the
    /// nudged flag (and would let the loop band go back to theme colour) when
    /// both are back to neutral.</summary>
    private void AfterAdjustment()
    {
        bool adjusted = _bpmOverride is not null || Math.Abs(_offsetSec) > 1e-9;
        SetGridNudged(adjusted);
        string? path = _currentFilePath();
        if (path is not null)
            _ = _gridCache.PutAsync(path, _bpmOverride, _offsetSec);
    }
}
