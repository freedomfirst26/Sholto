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
/// Deliberately minimal: identity, parameter metadata, a setter and an
/// audio-thread-owned reset. No <c>Mix</c>/wet-dry and no <c>IsEngaged</c> —
/// those need their own audio-behaviour proof and are separate tasks.
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

    private const int Normal = 0, FadingOut = 1, FadingIn = 2;

    private readonly int _rampFrames;
    private int _resetPending; // 0/1, Volatile/Interlocked-accessed
    // Audio-thread-only.
    private int _fadeStage;
    private float _fade = 1f;

    protected DeckEffect(AudioFormat format)
    {
        _rampFrames = Math.Max(1, (int)Math.Round(ResetRampSeconds * format.SampleRate));
    }

    /// <inheritdoc/>
    public abstract string EffectId { get; }

    /// <summary>This effect's parameter metadata. Implementations return a
    /// span over a <c>private static readonly EffectParam[]</c> table — never
    /// a per-call allocation.</summary>
    public abstract ReadOnlySpan<EffectParam> Params { get; }

    /// <summary>Apply a value to one parameter, addressed by the <c>Id</c>
    /// from its <see cref="EffectParam"/> entry in <see cref="Params"/>.
    /// Forwards to the effect's existing public setter — this method holds no
    /// state and no DSP maths of its own. Safe to call from any thread (the
    /// same guarantee as the setter it forwards to).</summary>
    public abstract void SetParam(int paramId, double value);

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

    /// <inheritdoc/>
    public sealed override void Process(Span<float> buffer, int channels)
    {
        if (_fadeStage == Normal && Volatile.Read(ref _resetPending) == 0)
        {
            ProcessBuffer(buffer, channels);
            return;
        }

        // A request taken while FadingOut is absorbed: ResetState has not run
        // yet and will run at the end of this fade. While FadingIn it starts
        // a new fade-out from the current _fade, without a jump.
        if (Interlocked.Exchange(ref _resetPending, 0) == 1 && _fadeStage != FadingOut)
            _fadeStage = FadingOut;

        ProcessBuffer(buffer, channels);

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
