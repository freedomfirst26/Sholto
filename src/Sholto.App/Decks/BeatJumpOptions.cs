namespace Sholto.App.Decks;

/// <summary>How far the BEAT JUMP size can be shrunk or grown, supplied via the standard
/// <c>IOptions&lt;BeatJumpOptions&gt;</c> pipeline. The size is a scale on each pad's own beats: 1 jumps
/// the pad's beats as printed, 0.5 half as far, 2 twice as far.</summary>
public sealed class BeatJumpOptions
{
    /// <summary>The smallest scale halving reaches (1/8: the 1-beat pads jump an eighth of a beat).</summary>
    public double MinScale { get; set; } = 0.125;

    /// <summary>The largest scale doubling reaches (8: the 8-beat pads jump 64 beats).</summary>
    public double MaxScale { get; set; } = 8;
}
