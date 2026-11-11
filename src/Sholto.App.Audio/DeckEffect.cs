using SoundFlow.Abstracts;
using SoundFlow.Structs;

namespace Sholto.App.Audio;

/// <summary>
/// Base for a deck effect that is addressable by a string id and a small set
/// of numeric parameters, rather than only by C# type. <see cref="DeckMixer.AttachModifiers"/>
/// still picks out the 3 hardware-bound effects (EQ/filter/echo) by type for
/// their named methods (<c>SetEq</c>/<c>SetFilter</c>/<c>SetEcho</c>) — that
/// lookup is unchanged and stays the hardware path. This is the ADDITIONAL
/// seam: any effect that is a <see cref="DeckEffect"/>, including one a
/// hardcoded type-lookup could never reach (e.g. a fourth effect added later
/// to <see cref="DeckEffectFactory"/>), becomes controllable via
/// <see cref="IDeckEffects.SetParam"/> once it's built into the chain.
///
/// Deliberately minimal: identity, parameter metadata, a setter, a wet/dry Mix
/// and an audio-thread-owned reset. No <c>IsEngaged</c> — that needs its own
/// audio-behaviour proof and is a separate task.
///
/// Mix and Reset: every effect lists one <see cref="MixParam"/> row (id
/// <see cref="MixParamId"/>, 0..1, default 1) and it is set only through
/// <see cref="SetParam"/>. The Mix blend and the cross-thread reset fade live in
/// the injected <see cref="IEffectOutputStage"/> (<see cref="EffectOutputStage"/>);
/// the sealed <see cref="Process"/> delegates to it, and it calls back into the
/// effect's DSP through <see cref="IEffectDsp"/>. <see cref="RequestReset"/> may be
/// called from any thread. <c>ProcessSample</c> ignores both Mix and reset.
///
/// <see cref="EffectId"/> and <see cref="Params"/> are control-thread-only
/// metadata: nothing on the per-buffer <c>Process</c>/<c>ProcessSample</c>
/// path may read them — that path stays exactly as fast and
/// allocation-free as it was before this change.
/// </summary>
public abstract class DeckEffect : SoundModifier, IDeckEffectInfo, IEffectDsp
{
    /// <summary>The Mix parameter id, the same on every effect and clear of the
    /// per-effect ids.</summary>
    public const int MixParamId = 100;

    private readonly IEffectOutputStage _outputStage;

    private protected DeckEffect(IEffectOutputStage outputStage)
    {
        _outputStage = outputStage;
    }

    /// <inheritdoc/>
    public abstract string EffectId { get; }

    /// <summary>This effect's parameter metadata. Implementations return a
    /// span over a per-instance table built in the constructor — never a
    /// per-call allocation.</summary>
    public abstract ReadOnlySpan<EffectParam> Params { get; }

    /// <summary>The Mix row every effect appends to its <see cref="Params"/>.</summary>
    protected EffectParam MixParam => new(MixParamId, "Mix", 0.0, 1.0, 1.0, EffectParamCurve.Linear);

    /// <summary>Apply a value to one parameter, addressed by the <c>Id</c>
    /// from its <see cref="EffectParam"/> entry in <see cref="Params"/>.
    /// <see cref="MixParamId"/> sets Mix here; every other id goes to
    /// <see cref="SetEffectParam"/>. Safe to call from any thread (the
    /// same guarantee as the setter it forwards to).</summary>
    public void SetParam(int paramId, double value)
    {
        if (paramId == MixParamId) _outputStage.SetMix(value);
        else SetEffectParam(paramId, value);
    }

    /// <summary>Apply a value to one of this effect's own parameters.
    /// Forwards to the effect's existing public setter — holds no state and no
    /// DSP maths of its own. Never called with <see cref="MixParamId"/>.</summary>
    protected abstract void SetEffectParam(int paramId, double value);

    /// <summary>True when the effect's DSP may be skipped while Mix is settled
    /// at 0 (its output is then exactly the input). The base calls
    /// <see cref="ResetState"/> once on entering the skip.</summary>
    protected virtual bool SkipDspAtMixZero => false;

    /// <summary>Ask the audio thread to flush this effect's DSP state. Any
    /// thread. Clears the on/off target immediately (see
    /// <see cref="ResetControlTargets"/>) so an enable issued afterwards
    /// survives; the state itself is cleared by <see cref="ResetState"/> on
    /// the audio thread after a ~5 ms fade to zero.</summary>
    public void RequestReset()
    {
        ResetControlTargets();
        _outputStage.RequestReset();
    }

    /// <summary>Calling thread: switch off the effect's on/off target (echo,
    /// roll). Default does nothing. Must not touch audio-thread state.</summary>
    protected virtual void ResetControlTargets() { }

    /// <summary>Audio thread only, no allocation: clear delay lines, filter
    /// memory and any other DSP state. Must never write the on/off target.</summary>
    protected abstract void ResetState();

    /// <summary>The effect's per-buffer DSP (what <c>Process</c> used to be).</summary>
    protected abstract void ProcessBuffer(Span<float> buffer, int channels);

    void IEffectDsp.ProcessBuffer(Span<float> buffer, int channels) => ProcessBuffer(buffer, channels);

    void IEffectDsp.ResetState() => ResetState();

    bool IEffectDsp.SkipDspAtMixZero => SkipDspAtMixZero;

    /// <inheritdoc/>
    public sealed override void Process(Span<float> buffer, int channels)
        => _outputStage.Process(buffer, channels, this);
}
