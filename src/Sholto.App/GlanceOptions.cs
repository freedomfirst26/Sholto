namespace Sholto.App;

/// <summary>Glance search settings, supplied via the standard <c>IOptions&lt;GlanceOptions&gt;</c> pipeline like
/// <see cref="ScratchOptions"/>. Defaults live here.</summary>
public sealed class GlanceOptions
{
    /// <summary>The most rows a Glance rank returns, best first. The footer still reports the full scope size,
    /// so a capped list reads "250 of 1,234".</summary>
    public int MaxResults { get; set; } = 250;
}
