namespace Sholto.Interface.MainUI.Controls.WaveformStyles;

/// <summary>A track's waveform body reduced to coarse bins of pixel heights (half-height, mirrored about
/// the centre line), ready to fill. <see cref="Mono"/> is set instead of the three bands when the peaks
/// carry no band data. Heights are already gated, scaled and run through the attack/release follower.</summary>
public sealed record BandEnvelopes(
    int Width,
    int Height,
    float MidY,
    int BinPx,
    float[]? Mono,
    float[] Low,
    float[] Mid,
    float[] High);
