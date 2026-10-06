namespace Sholto.App.Analysis.Analyzers.Segments;

/// <summary>Per-bar features of a track. Band levels are relative to the track's sustained
/// maximum for that band (1.0 = as loud as the loud parts get; may exceed 1 briefly).
/// All arrays have <see cref="Bars"/> entries.</summary>
/// <param name="Low">Low-band level.</param>
/// <param name="Mid">Mid-band level.</param>
/// <param name="High">High-band level.</param>
/// <param name="Kick">Share of the bar's beats that carry a kick, 0..1.</param>
/// <param name="Bass">Bass presence, 0..1.</param>
/// <param name="Flux">Spectral change inside the bar, relative to the track's typical bar, 0..~1.5.</param>
public sealed record BarFeatures(float[] Low, float[] Mid, float[] High, float[] Kick, float[] Bass, float[] Flux)
{
    public int Bars => Low.Length;

    /// <summary>Mean of low, mid and high for <paramref name="bar"/>.</summary>
    public float Loudness(int bar) => (Low[bar] + Mid[bar] + High[bar]) / 3f;
}
