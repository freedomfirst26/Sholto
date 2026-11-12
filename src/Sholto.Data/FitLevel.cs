namespace Sholto.Data;

/// <summary>How well a track mixes with the reference deck.</summary>
/// <remarks>
/// The graded-fit vocabulary of the <see cref="RankedTracks"/> event. It lives in
/// Sholto.Data because <see cref="RankedTrack"/> carries it to the interfaces, which
/// may reference only Data; the interfaces read it (and MainUI's Glance rows reuse it
/// to grade the tempo delta too), while the scoring that decides it stays in
/// Sholto.App (<c>FitScorer</c>).
/// </remarks>
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
