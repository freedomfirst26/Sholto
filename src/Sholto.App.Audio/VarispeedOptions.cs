namespace Sholto.App.Audio;

/// <summary>How the in-memory playback providers (<see cref="ScratchDataProvider"/>,
/// <see cref="StemMixDataProvider"/>) read the track at a signed vinyl-style speed,
/// supplied via the standard <c>IOptions&lt;VarispeedOptions&gt;</c> pipeline and read once
/// in each provider's constructor.</summary>
public sealed class VarispeedOptions
{
    /// <summary>The fastest the reader moves through the track, in either direction, as a
    /// multiple of normal speed. A requested speed beyond it (a hard scratch fling or
    /// backspin asks for up to <c>ScratchOptions.MaxLaunchRate</c>) is read at this speed
    /// instead. Default 8: past that a backspin is pure whoosh and the box filter would
    /// average ever more source frames per output frame for no audible gain.</summary>
    public double MaxSpeed { get; set; } = 8;
}
