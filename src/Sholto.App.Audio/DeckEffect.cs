using System.Threading;
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
/// Mix: every effect lists one <see cref="MixParam"/> row (id
/// <see cref="MixParamId"/>, 0..1, default 1) and it is set only through
/// <see cref="SetParam"/>. The sealed <see cref="Process"/> blends
/// <c>dry + (wet - dry) * mix</c> once, here, for every effect, with a linear
/// per-sample ramp of ~5 ms. While Mix is settled at 1 it calls
/// <see cref="ProcessBuffer"/> exactly as before, so the output is bit-identical
/// to an effect that has no Mix. Effects whose
/// <see cref="SkipDspAtMixZero"/> is true (EQ, filter) skip their DSP at
/// settled Mix 0 and are reset once on entering it; echo and roll keep running
/// their state, so a tail or a held roll is there when Mix returns. Reset and
/// <c>ProcessSample</c> leave Mix alone; <c>ProcessSample</c> ignores it.
///
/// Reset: <see cref="RequestReset"/> may be called from any thread. The
/// sealed <see cref="Process"/> then fades the effect output to zero over
/// ~5 ms, runs <see cref="ResetState"/> on the audio thread, and fades back
/// in over ~5 ms. Nothing is cleared on the calling thread. The reset is
/// honoured only on the <c>Process</c> path, which is the playback path;
/// <c>ProcessSample</c> ignores it. The fade scales the whole effect output,
/// dry signal included, so during a reset the deck is silent for the rest of
/// the buffer in which the fade reaches zero plus the 5 ms fade-in (up to
/// about 26 ms with a 1024-frame buffer). This is intended at track load,
/// not a bug.
///
/// <see cref="EffectId"/> and <see cref="Params"/> are control-thread-only
/// metadata: nothing on the per-buffer <c>Process</c>/<c>ProcessSample</c>
/// path may read them — that path stays exactly as fast and
/// allocation-free as it was before this change.
/// </summary>
public abstract class DeckEffect : SoundModifier, IDeckEffectInfo
{
    // Declick guard, not a DJ-tunable value (matches TempoMath.FiveMsGainAlpha's 5 ms).
    private const double ResetRampSeconds = 0.005;

    /// <summary>The Mix parameter id, the same on every effect and clear of the
    /// per-effect ids.</summary>
    public const int MixParamId = 100;

    private const int ScratchFrames = 256;

    private const int Normal = 0, FadingOut = 1, FadingIn = 2;

    private readonly int _rampFrames;
    private int _resetPending; // 0/1, Volatile/Interlocked-accessed
    // Audio-thread-only.
    private int _fadeStage;
    private float _fade = 1f;
    private float _mixTarget = 1f; // any thread, Volatile-accessed
    // Audio-thread-only.
    private float _mix = 1f;
    private bool _mixSkipping;
    private readonly float[] _dryScratch;

    protected DeckEffect(AudioFormat format)
    {
        _dryScratch = new float[ScratchFrames * Math.Max(1, format.Channels)];
        _rampFrames = Math.Max(1, (int)Math.Round(ResetRampSeconds * format.SampleRate));
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
        if (paramId == MixParamId) SetMixTarget(value);
        else SetEffectParam(paramId, value);
    }

    /// <summary>Apply a value to one of this effect's own parameters.
    /// Forwards to the effect's existing public setter — holds no state and no
    /// DSP maths of its own. Never called with <see cref="MixParamId"/>.</summary>
    protected abstract void SetEffectParam(int paramId, double value);

    private void SetMixTarget(double value)
    {
        if (double.IsNaN(value)) return;
        Volatile.Write(ref _mixTarget, (float)Math.Clamp(value, 0.0, 1.0));
    }

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
        Volatile.Write(ref _resetPending, 1);
    }

    /// <summary>Calling thread: switch off the effect's on/off target (echo,
    /// roll). Default does nothing. Must not touch audio-thread state.</summary>
    protected virtual void ResetControlTargets() { }

    /// <summary>Audio thread only, no allocation: clear delay lines, filter
    /// memory and any other DSP state. Must never write the on/off target.</summary>
    protected abstract void ResetState();

    /// <summary>The effect's per-buffer DSP (what <c>Process</c> used to be).</summary>
    protected abstract void ProcessBuffer(Span<float> buffer, int channels);

    private void ProcessMixed(Span<float> buffer, int channels)
    {
        float mt = Volatile.Read(ref _mixTarget);
        if (_mix == 1f && mt == 1f)
        {
            _mixSkipping = false;
            ProcessBuffer(buffer, channels);
            return;
        }
        if (_mix == 0f && mt == 0f && SkipDspAtMixZero)
        {
            if (!_mixSkipping)
            {
                ResetState();
                _mixSkipping = true;
            }
            return;
        }
        _mixSkipping = false;

        float step = 1f / _rampFrames;
        int chunkFrames = Math.Max(1, _dryScratch.Length / channels);
        int frames = buffer.Length / channels;
        for (int start = 0; start < frames; start += chunkFrames)
        {
            int n = Math.Min(chunkFrames, frames - start);
            var chunk = buffer.Slice(start * channels, n * channels);
            var dry = _dryScratch.AsSpan(0, n * channels);
            chunk.CopyTo(dry);
            ProcessBuffer(chunk, channels);
            for (int i = 0; i < n; i++)
            {
                if (_mix < mt) _mix = MathF.Min(mt, _mix + step);
                else if (_mix > mt) _mix = MathF.Max(mt, _mix - step);
                int b = i * channels;
                for (int ch = 0; ch < channels; ch++)
                    chunk[b + ch] = dry[b + ch] + (chunk[b + ch] - dry[b + ch]) * _mix;
            }
        }
    }

    /// <inheritdoc/>
    public sealed override void Process(Span<float> buffer, int channels)
    {
        if (_fadeStage == Normal && Volatile.Read(ref _resetPending) == 0)
        {
            ProcessMixed(buffer, channels);
            return;
        }

        // A request taken while FadingOut is absorbed: ResetState has not run
        // yet and will run at the end of this fade. While FadingIn it starts
        // a new fade-out from the current _fade, without a jump.
        if (Interlocked.Exchange(ref _resetPending, 0) == 1 && _fadeStage != FadingOut)
            _fadeStage = FadingOut;

        ProcessMixed(buffer, channels);

        float step = 1f / _rampFrames;
        int frames = buffer.Length / channels;
        for (int i = 0; i < frames; i++)
        {
            int baseIdx = i * channels;
            if (_fadeStage == FadingOut)
            {
                _fade = MathF.Max(0f, _fade - step);
                for (int ch = 0; ch < channels; ch++) buffer[baseIdx + ch] *= _fade;
                if (_fade == 0f)
                {
                    ResetState();
                    buffer.Slice(baseIdx + channels, buffer.Length - baseIdx - channels).Clear();
                    _fadeStage = FadingIn;
                    return;
                }
            }
            else if (_fadeStage == FadingIn)
            {
                _fade = MathF.Min(1f, _fade + step);
                for (int ch = 0; ch < channels; ch++) buffer[baseIdx + ch] *= _fade;
                if (_fade == 1f) { _fadeStage = Normal; return; }
            }
            else return;
        }
    }
}
