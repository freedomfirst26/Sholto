namespace Sholto.Data;

/// <summary>How well a track mixes with the reference deck.</summary>
public enum FitLevel
{
    /// <summary>No fit bar (no reference, or not enough data).</summary>
    None,

    /// <summary>Keys or tempo clash (grey bar).</summary>
    Clash,

    /// <summary>Mixable with care (amber bar).</summary>
    Usable,

    /// <summary>Mixes well (green bar).</summary>
    Good,
}
