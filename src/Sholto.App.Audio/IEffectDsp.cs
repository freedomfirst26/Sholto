namespace Sholto.App.Audio;

/// <summary>
/// The effect's own DSP, as the <see cref="IEffectOutputStage"/> calls it. Audio
/// thread only, no allocation. Called at most once per 256-frame chunk and once
/// per reset, never per sample.
/// </summary>
internal interface IEffectDsp
{
    /// <summary>The effect's per-buffer DSP.</summary>
    void ProcessBuffer(Span<float> buffer, int channels);

    /// <summary>Clear delay lines, filter memory and any other DSP state.</summary>
    void ResetState();

    /// <summary>True when the DSP may be skipped while Mix is settled at 0.</summary>
    bool SkipDspAtMixZero { get; }
}
