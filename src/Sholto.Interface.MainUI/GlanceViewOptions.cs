namespace Sholto.Interface.MainUI;

/// <summary>Glance overlay behaviour, supplied via the standard <c>IOptions&lt;GlanceViewOptions&gt;</c> pipeline like
/// <see cref="FeatureOptions"/>. Defaults live here.</summary>
public sealed class GlanceViewOptions
{
    /// <summary>The most tags the rail (and tag completion) offers at once.</summary>
    public int TagRailLimit { get; set; } = 10;

    /// <summary>How long, in milliseconds, a ranking the person asked for may run before the slow indicator shows.</summary>
    public int IndicatorDelayMs { get; set; } = 150;

    /// <summary>The shortest time, in milliseconds, the slow indicator stays on once shown.</summary>
    public int IndicatorMinimumMs { get; set; } = 300;

    /// <summary>How long, in milliseconds, a first Ctrl+Delete waits for its second.</summary>
    public int ClearArmMs { get; set; } = 2000;

    /// <summary>A playing LOAD TO slot with less than this many seconds left turns its ring red.</summary>
    public double LowTimeSeconds { get; set; } = 45;

    /// <summary>Tempo the LOAD TO platter turns at when the deck has no analysed BPM. Keep equal to
    /// <c>ScratchOptions.FallbackBpm</c>; <c>SholtoOptions</c> seeds it from there.</summary>
    public double FallbackBpm { get; set; } = 120;
}
