namespace Sholto.Audio;

/// <summary>
/// An active auto-beat-loop region in interleaved-sample units. Stereo, so
/// both bounds are always even. Held by a data provider as <c>LoopRegion?</c>:
/// <c>null</c> = no loop, struct = wrap reads at <see cref="EndSample"/> back
/// to <see cref="StartSample"/>.
///
/// Sample units (not seconds) so the audio thread never multiplies by the
/// sample rate per buffer to decide whether to wrap — the comparison is a
/// plain long &lt; long.
/// </summary>
public readonly record struct LoopRegion(long StartSample, long EndSample)
{
    public long LengthSamples => EndSample - StartSample;
}
