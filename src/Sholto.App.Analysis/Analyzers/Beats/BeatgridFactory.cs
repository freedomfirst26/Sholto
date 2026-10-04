namespace Sholto.App.Analysis.Analyzers.Beats;

/// <summary>Owns the named <see cref="Beatgrid"/> recipes, so no call site spells out
/// what "a grid that describes nothing" is. Built once at the composition root and
/// injected; holds no mutable state.</summary>
public sealed class BeatgridFactory : IBeatgridFactory
{
    private readonly Beatgrid _none;

    public BeatgridFactory()
    {
        // No anchor, no tempo, 4 beats per bar, no duration: Beatgrid.IsEmpty is true.
        _none = new Beatgrid(0.0, 0.0, 4, 0.0);
    }

    /// <summary>The grid that describes nothing renderable. The same instance every
    /// call (a <see cref="Beatgrid"/> is immutable).</summary>
    public Beatgrid None() => _none;
}
