namespace Sholto.App;

/// <summary>Feature flags supplied via the standard <c>IOptions&lt;FeatureOptions&gt;</c>
/// pipeline. Defaults live here; a config source can override them later without
/// touching the view models.</summary>
public sealed class FeatureOptions
{
    /// <summary>Whether the stationary whole-song section "map" strip is shown above
    /// each deck. Disabled for now: the map's sections are built on top of the
    /// beatgrid, and the beatgrid needs to be verified correct before we build the
    /// power/vibe segmentation on it (bar-aligned sections poison if the grid is
    /// wrong). Flip to true — or override via config — to re-enable.</summary>
    public bool ShowSectionMap { get; set; } = false;
}
