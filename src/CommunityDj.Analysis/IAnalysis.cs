namespace CommunityDj.Analysis;

/// <summary>
/// Marker interface for a single piece of track analysis (waveform, BPM, key, energy, …).
/// </summary>
public interface IAnalysis
{
    /// <summary>Human-readable name, e.g. "Basic", "Key". Used in UI / debug logs.</summary>
    string Name { get; }
}
