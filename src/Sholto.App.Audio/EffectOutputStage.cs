using System.Threading;
using SoundFlow.Structs;

namespace Sholto.App.Audio;

/// <summary>
/// Mix: <see cref="Process"/> blends <c>dry + (wet - dry) * mix</c> once, for every
/// effect, with a linear per-sample ramp of ~5 ms. While Mix is settled at 1 it calls
/// <see cref="IEffectDsp.ProcessBuffer"/> exactly as before, so the output is bit-identical
/// to an effect that has no Mix. Effects whose <see cref="IEffectDsp.SkipDspAtMixZero"/>
/// is true (EQ, filter) skip their DSP at settled Mix 0 and are reset once on entering it;
/// echo and roll keep running their state, so a tail or a held roll is there when Mix
/// returns.
///
/// Reset: <see cref="RequestReset"/> may be called from any thread. <see cref="Process"/>
/// then fades the effect output to zero over ~5 ms, runs <see cref="IEffectDsp.ResetState"/>
/// on the audio thread, and fades back in over ~5 ms. Nothing is cleared on the calling
/// thread. The fade scales the whole effect output, dry signal included, so during a reset
/// the deck is silent for the rest of the buffer in which the fade reaches zero plus the
/// 5 ms fade-in (up to about 26 ms with a 1024-frame buffer). This is intended at track
/// load, not a bug.
/// </summary>
internal sealed class EffectOutputStage : IEffectOutputStage
{
    // Declick guard, not a DJ-tunable value (matches TempoMath.FiveMsGainAlpha's 5 ms).
    private const double ResetRampSeconds = 0.005;

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

    public EffectOutputStage(AudioFormat format)
    {
        _dryScratch = new float[ScratchFrames * Math.Max(1, format.Channels)];
        _rampFrames = Math.Max(1, (int)Math.Round(ResetRampSeconds * format.SampleRate));
    }

    /// <inheritdoc/>
    public void SetMix(double value)
    {
        if (double.IsNaN(value)) return;
        Volatile.Write(ref _mixTarget, (float)Math.Clamp(value, 0.0, 1.0));
    }

    /// <inheritdoc/>
    public void RequestReset() => Volatile.Write(ref _resetPending, 1);

    private void ProcessMixed(Span<float> buffer, int channels, IEffectDsp dsp)
    {
        float mt = Volatile.Read(ref _mixTarget);
        if (_mix == 1f && mt == 1f)
        {
            _mixSkipping = false;
            dsp.ProcessBuffer(buffer, channels);
            return;
        }
        if (_mix == 0f && mt == 0f && dsp.SkipDspAtMixZero)
        {
            if (!_mixSkipping)
            {
                dsp.ResetState();
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
            dsp.ProcessBuffer(chunk, channels);
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
    public void Process(Span<float> buffer, int channels, IEffectDsp dsp)
    {
        if (_fadeStage == Normal && Volatile.Read(ref _resetPending) == 0)
        {
            ProcessMixed(buffer, channels, dsp);
            return;
        }

        // A request taken while FadingOut is absorbed: ResetState has not run
        // yet and will run at the end of this fade. While FadingIn it starts
        // a new fade-out from the current _fade, without a jump.
        if (Interlocked.Exchange(ref _resetPending, 0) == 1 && _fadeStage != FadingOut)
            _fadeStage = FadingOut;

        ProcessMixed(buffer, channels, dsp);

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
                    dsp.ResetState();
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
