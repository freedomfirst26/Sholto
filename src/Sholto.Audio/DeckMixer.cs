using Sholto.Analysis;
using SoundFlow.Abstracts;
using SoundFlow.Modifiers;

namespace Sholto.Audio;

/// <summary>
/// Post-mix output controls, extracted out of <see cref="Deck"/>: fader
/// volume/master-path gain and PFL cue (<see cref="IDeckMixer"/>), plus the
/// post-mix DSP chain — EQ, filter, echo (<see cref="IDeckEffects"/>). Same
/// class implementing both ports for now; the interfaces are split (see each
/// port's doc for why) but the underlying state — gain and the 3 effect
/// references — has always lived together here and splitting the class isn't
/// needed to get the seam right.
///
/// The chain's modifiers can't be constructed here — they need the SoundFlow
/// engine/format that only exists once <c>Deck.AttachEngine</c> runs (and
/// they're added onto Deck's own mixer there, which stays Deck's job —
/// transport/graph wiring is a later stage's cluster, not this one).
/// <c>AttachEngine</c> builds them from the ordered
/// <see cref="DeckEffectFactories"/> list and hands the whole built chain in
/// once via <see cref="AttachModifiers"/>, which picks out the 3 it knows
/// about by type — a 4th (or 5th) factory in the list is simply not one of
/// the types it looks for, so no change is needed here to add one. Every
/// SetEq/SetFilter/SetEcho call before <see cref="AttachModifiers"/> lands is
/// a silent no-op, same as before extraction.
///
/// <see cref="_analysis"/> (for the echo's beat-synced tempo) and
/// <see cref="_playbackSpeed"/> (the live tempo-fader multiplier the echo's
/// delay time must track) are both owned by Deck / a sibling component
/// (<c>DeckTempo</c>), so they're handed in as narrow accessor
/// delegates rather than via a back-reference to Deck.</summary>
internal sealed class DeckMixer : IDeckMixer, IDeckEffects
{
    /// <summary>Assumed BPM before analysis has landed (or if analysis never
    /// reports one). Shared with <see cref="EchoEffect"/> and
    /// <see cref="BeatRepeatEffect"/> so a pre-analysis echo and a
    /// pre-analysis roll agree on tempo.</summary>
    internal const double DefaultBpm = 128.0;

    private readonly Func<TrackAnalysis> _analysis;
    private readonly Func<float> _playbackSpeed;

    public DeckMixer(Func<TrackAnalysis> analysis, Func<float> playbackSpeed)
    {
        _analysis = analysis;
        _playbackSpeed = playbackSpeed;
    }

    private BiquadEq3Band? _eq;
    private DjFilter? _filter;
    private EchoEffect? _echo;

    // Built once in AttachModifiers, on the control thread; read only by
    // control threads (SetParam / Effects) — never touched from Process.
    private Dictionary<string, DeckEffect> _effectsById = new();
    private IReadOnlyList<IDeckEffectInfo> _effectsList = [];

    // Tempo-consuming effects other than the echo (currently just the roll),
    // as target delegates built once here — not a common interface, since
    // EchoEffect/BeatRepeatEffect predate the DeckEffect seam and aren't
    // touched by this change. Picked out of _effectsById (already built
    // below) rather than a 4th OfType<T> call. PushTempo loops over this
    // plain array with no further type checks, LINQ, or allocation.
    private Action<double>[] _tempoTargets = [];

    /// <summary>Wire the post-mix chain Deck builds in <c>AttachEngine</c>
    /// from <see cref="DeckEffectFactories"/>, once its mixer exists. Picks
    /// out the 3 modifiers this class knows how to drive by type — anything
    /// else in <paramref name="effects"/> (a 4th/5th effect) is simply not
    /// matched by any of the three lookups below, so no change is needed
    /// here when the factory list grows. Not on <see cref="IDeckMixer"/> or
    /// <see cref="IDeckEffects"/> — only Deck calls this, right after
    /// constructing the chain and adding every modifier to its own
    /// mixer.</summary>
    internal void AttachModifiers(IReadOnlyList<SoundModifier> effects)
    {
        _eq = effects.OfType<BiquadEq3Band>().FirstOrDefault();
        _filter = effects.OfType<DjFilter>().FirstOrDefault();
        _echo = effects.OfType<EchoEffect>().FirstOrDefault();

        _effectsById = effects.OfType<DeckEffect>().ToDictionary(e => e.EffectId);
        _effectsList = _effectsById.Values.Cast<IDeckEffectInfo>().ToList();

        // Everything in the dictionary above that also tracks tempo, except
        // the echo (kept on its own path below because it alone needs the
        // "only while engaged" gate). Adding a further tempo-consuming
        // effect to DeckEffectFactories means adding one more case here —
        // same cost as the _eq/_filter/_echo type-lookups above — not
        // touching PushTempo.
        var tempoTargets = new List<Action<double>>(_effectsById.Count);
        foreach (var effect in _effectsById.Values)
        {
            if (effect is BeatRepeatEffect roll) tempoTargets.Add(roll.SetTempo);
        }
        _tempoTargets = tempoTargets.ToArray();
    }

    private float _volume = 1.0f;
    /// <inheritdoc/>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            ApplyDeckGain();
        }
    }

    /// <summary>Apply Volume to the master-path gain. (Stem-level mute is
    /// handled inside the data provider; the SoundPlayer itself stays at
    /// unity — see Deck.Load/LoadStreaming/SwitchToStemMode — so the
    /// headphone cue mix is genuinely pre-fader.)</summary>
    private void ApplyDeckGain() => Volatile.Write(ref _masterGain, _volume);

    // Master-path gain (channel × crossfade), read lock-free by CueOutputRouter.
    private float _masterGain = 1.0f;
    /// <inheritdoc/>
    public float MasterGain => Volatile.Read(ref _masterGain);

    /// <summary>Re-sync the master-path gain to the current Volume. Called by
    /// Deck after (re)building the SoundPlayer on Load/LoadStreaming/
    /// SwitchToStemMode, so a fresh player carries the fader's current
    /// attenuation forward instead of bursting in at unity. Not on
    /// <see cref="IDeckMixer"/>.</summary>
    internal void ReapplyGain() => ApplyDeckGain();

    // PFL cue selection — toggled by the deck CUE button. When true this deck is
    // summed into the headphone (ch3-4) mix at full level, pre-fader.
    private volatile bool _cueActive;
    /// <inheritdoc/>
    public event Action<bool>? CueChanged;
    /// <inheritdoc/>
    public bool CueActive
    {
        get => _cueActive;
        set { if (_cueActive == value) return; _cueActive = value; CueChanged?.Invoke(value); }
    }
    /// <inheritdoc/>
    public void ToggleCue() => CueActive = !CueActive;

    /// <inheritdoc/>
    public void SetEq(int band, double value)
    {
        // _eq lands via AttachModifiers once Deck.AttachEngine runs. If MIDI
        // arrives before that (shouldn't, but) just bail silently.
        if (_eq is null) return;

        // Isolator gain: 0 → mute, 0.5 → unity (1.0), 1 → +6 dB (2.0).
        // Curve is intentionally linear so the centre detent at v=0.5 reads as flat.
        double v = Math.Clamp(value, 0.0, 1.0);
        float gain = v < 0.5
            ? (float)(v * 2.0)
            : (float)(1.0 + (v - 0.5) * 2.0);
        _eq?.SetBandGain(band, gain);
    }

    /// <inheritdoc/>
    public void SetFilter(double position)
        => _filter?.SetPosition((float)Math.Clamp(position, 0.0, 1.0));

    /// <inheritdoc/>
    public bool EchoActive { get; private set; }

    /// <inheritdoc/>
    public void SetEcho(bool on)
    {
        if (_echo is null) return;
        if (on) PushTempo();
        _echo.SetEnabled(on);
        EchoActive = on;
    }

    /// <summary>Re-derive the effective BPM (source analysis BPM × live
    /// playback speed — 128 default if analysis hasn't landed yet) and push
    /// it to every tempo-consuming effect. The echo is pushed only while
    /// <see cref="EchoActive"/> — same "only when engaged" behaviour as
    /// before this was generalised. Every other tempo target in
    /// <see cref="_tempoTargets"/> (currently just the roll) is pushed
    /// unconditionally: unlike the echo it has no persistent "engaged" state
    /// worth gating on (it's only ever live for the instant a pad is held),
    /// and recomputing its period is cheap — see
    /// <see cref="BeatRepeatEffect.SetTempo"/> — so pushing it while
    /// disengaged is harmless and keeps it ready for the next press. Called
    /// from <see cref="SetEcho"/> on the on-transition, and again from
    /// <see cref="OnPlaybackSpeedChanged"/> / <see cref="OnAnalysisUpdated"/>
    /// so every target keeps tracking tempo/BPM changes live — see those
    /// methods' docs for why both are needed. Runs on the control thread;
    /// no allocation (fixed-size <see cref="_tempoTargets"/> array, no
    /// LINQ) and no locks.</summary>
    private void PushTempo()
    {
        double bpm = _analysis().Basic?.Bpm ?? DefaultBpm;
        double effectiveBpm = bpm * _playbackSpeed();

        if (_echo is not null && EchoActive) _echo.SetTempo(effectiveBpm);

        var targets = _tempoTargets;
        for (int i = 0; i < targets.Length; i++) targets[i](effectiveBpm);
    }

    /// <summary>Deck forwards <c>DeckTempo.PlaybackSpeedChanged</c> here (see
    /// Deck's constructor) so a live tempo-fader/BpmMultiplier move re-derives
    /// every tempo target's delay instead of leaving it pinned to whatever
    /// speed was in effect when it last picked up a tempo.</summary>
    internal void OnPlaybackSpeedChanged(float speed)
    {
        _ = speed; // current speed is read fresh via _playbackSpeed() inside PushTempo
        PushTempo();
    }

    /// <summary>Deck forwards its <c>AnalysisUpdated</c> event here so a BPM
    /// that lands after a tempo target was already engaged (analysis takes
    /// seconds; engaging one before BasicAnalysis completes pins it to the
    /// 128 default otherwise) gets picked up instead of staying stale for the
    /// rest of the track.</summary>
    internal void OnAnalysisUpdated()
    {
        PushTempo();
    }

    /// <inheritdoc/>
    public void ResetEcho()
    {
        _echo?.Reset();
        EchoActive = false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IDeckEffectInfo> Effects => _effectsList;

    /// <inheritdoc/>
    public void SetParam(string effectId, int paramId, double value)
    {
        // Same tolerance as SetEq/SetFilter/SetEcho above: silently a no-op
        // before AttachModifiers has run, and for an unknown effect id.
        if (_effectsById.TryGetValue(effectId, out var effect))
            effect.SetParam(paramId, value);
    }
}
