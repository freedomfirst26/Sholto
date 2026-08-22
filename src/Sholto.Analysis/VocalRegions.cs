namespace Sholto.Analysis;

/// <summary>One span of a track where the vocal is present, in track seconds.</summary>
public readonly record struct VocalRegion(double StartSec, double EndSec);

/// <summary>Where the vocals sit in a track — a handful of presence spans derived
/// from the isolated vocal stem by <see cref="VocalRegionAnalyzer"/>. The deck's
/// waveform paints these as solid green rectangles: a presence layer on top of the
/// frequency bands, deliberately NOT a waveform. One per track, once stems land.</summary>
public sealed record VocalRegions(IReadOnlyList<VocalRegion> Regions) : IAnalysis
{
    public string Name => "VocalRegions";
    public static VocalRegions Empty { get; } = new(System.Array.Empty<VocalRegion>());
}
