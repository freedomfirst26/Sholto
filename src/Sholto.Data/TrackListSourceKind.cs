namespace Sholto.Data;

/// <summary>Where a Track List source came from.</summary>
public enum TrackListSourceKind
{
    /// <summary>A crate snapshot.</summary>
    Crate,

    /// <summary>A tag snapshot.</summary>
    Tag,

    /// <summary>Individually loaded songs.</summary>
    Songs,
}
