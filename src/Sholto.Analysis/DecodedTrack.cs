namespace Sholto.Analysis;

/// <summary>
/// Completely describes a decoded track: its identity (the file path, used as
/// the cache key), its decoded samples, and everything needed to interpret
/// that buffer — the sample rate and channel count it was decoded at.
/// </summary>
public sealed record DecodedTrack(string FilePath, float[] StereoSamples, int SampleRate, int Channels);
