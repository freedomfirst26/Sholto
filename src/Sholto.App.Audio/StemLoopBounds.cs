namespace Sholto.App.Audio;

/// <summary>
/// Active loop region in interleaved-sample units. Published by
/// <see cref="StemMixDataProvider"/> as one immutable reference (same pattern
/// as <see cref="StemLoopTail"/>) so the audio thread can never see a start
/// from one region paired with an end from another. The holder is allocated on
/// the control thread in SetLoop; the audio thread only reads it, once per
/// buffer.
/// </summary>
internal sealed class StemLoopBounds(long start, long end)
{
    public long Start { get; } = start;
    public long End { get; } = end;
}
