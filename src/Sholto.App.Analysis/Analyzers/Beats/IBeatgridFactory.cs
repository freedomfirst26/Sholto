namespace Sholto.App.Analysis.Analyzers.Beats;

/// <summary>Owns the named <see cref="Beatgrid"/> recipes.</summary>
public interface IBeatgridFactory
{
    /// <summary>The grid that describes nothing renderable.</summary>
    Beatgrid None();
}
