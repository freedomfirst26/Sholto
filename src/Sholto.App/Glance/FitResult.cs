using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>The outcome of scoring one track against the reference.</summary>
/// <param name="Level">The bar colour.</param>
/// <param name="Score">Key score plus tempo score (clamped to 1 past the 6% limit); -1 when there is no fit.</param>
/// <param name="TempoDeltaPercent">Signed tempo difference from the reference after half/double-time folding; null when unknown.</param>
public readonly record struct FitResult(FitLevel Level, int Score, double? TempoDeltaPercent);
