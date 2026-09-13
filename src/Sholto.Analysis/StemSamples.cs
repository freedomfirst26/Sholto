namespace Sholto.Analysis;

/// <summary>The four decoded stem sample buffers for one track — interleaved
/// stereo float32, one per stem. These only ever travel together: mixing
/// needs all four to be from the same track, at the same decode, in lockstep,
/// so they're carried as one value rather than four loose parameters.</summary>
public sealed record StemSamples(float[] Drums, float[] Vocals, float[] Bass, float[] Other);
