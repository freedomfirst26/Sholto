namespace Sholto.Data;

/// <summary>One row of <see cref="RankedTracks"/>.</summary>
/// <param name="Summary">The library row.</param>
/// <param name="Fit">How well it mixes with the reference deck.</param>
/// <param name="TempoDeltaPercent">Tempo difference from the reference, in percent; null when unknown.</param>
/// <param name="IsReference">True if this is the track loaded in the reference deck.</param>
public readonly record struct RankedTrack(TrackSummary Summary, FitLevel Fit, double? TempoDeltaPercent, bool IsReference);
