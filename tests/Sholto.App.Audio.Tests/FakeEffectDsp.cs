namespace Sholto.App.Audio.Tests;

/// <summary>Test double for <see cref="IEffectDsp"/>: doubles the signal and counts calls.</summary>
internal sealed class FakeEffectDsp : IEffectDsp
{
    public int ProcessCount { get; private set; }
    public int ResetCount { get; private set; }
    public bool Skip { get; set; }

    public bool SkipDspAtMixZero => Skip;

    public void ProcessBuffer(Span<float> buffer, int channels)
    {
        ProcessCount++;
        for (int i = 0; i < buffer.Length; i++) buffer[i] *= 2f;
    }

    public void ResetState() => ResetCount++;
}
