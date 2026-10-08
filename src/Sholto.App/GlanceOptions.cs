namespace Sholto.App;

/// <summary>Glance search settings, supplied via the standard <c>IOptions&lt;GlanceOptions&gt;</c> pipeline like
/// <see cref="ScratchOptions"/>. Defaults live here.</summary>
public sealed class GlanceOptions
{
    /// <summary>The most rows a Glance rank returns, best first. The footer still reports the full scope size,
    /// so a capped list reads "250 of 1,234".</summary>
    public int MaxResults { get; set; } = 250;

    /// <summary>Tempo gap, in percent of the reference tempo (half and double time count as no gap), beyond which
    /// a candidate is a clash whatever its key.</summary>
    public double ClashTempoPercent { get; set; } = 6.0;

    /// <summary>Tempo gap, in percent, at or under which a candidate scores the full tempo points.</summary>
    public double GoodTempoPercent { get; set; } = 2.0;

    /// <summary>Tempo gap, in percent, at or under which a candidate scores part of the tempo points.</summary>
    public double UsableTempoPercent { get; set; } = 4.0;
}
