namespace Sholto.Interface.MainUI;

/// <summary>Feature flags supplied via the standard <c>IOptions&lt;FeatureOptions&gt;</c>
/// pipeline. Defaults live here; a config source can override them later without
/// touching the view models.</summary>
public sealed class FeatureOptions
{
    /// <summary>Whether the stationary whole-song section "map" strip is shown above
    /// each deck. Originally parked while the beatgrid was unverified (bar-aligned
    /// sections poison on a wrong grid); re-enabled once the beatgrid became a
    /// constant-spacing grid synthesized from the beat detections.</summary>
    public bool ShowSectionMap { get; set; } = true;
}
