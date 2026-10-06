namespace Sholto.App.Tests.Segments;

/// <summary>Per-band level [0..1] of one waveform column.</summary>
internal readonly record struct SyntheticBandLevels(float Low, float Mid, float High);
