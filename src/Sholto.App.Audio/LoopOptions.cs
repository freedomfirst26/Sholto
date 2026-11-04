namespace Sholto.App.Audio;

/// <summary>Beat-loop behaviour, supplied via the standard <c>IOptions&lt;LoopOptions&gt;</c>
/// pipeline. The <c>SHOLTO_LOOP_MODE</c> and <c>SHOLTO_LOOP_FADE_MS</c> environment variables
/// still override the defaults, read once at the composition root (<c>LoopOptionsFactory</c>).</summary>
public sealed class LoopOptions
{
    /// <summary>Loop-in / period strategy: <c>beat-snap</c> (default), <c>downbeat</c>, <c>bpm</c>,
    /// or <c>no-snap</c> / <c>tap</c>. See <c>DeckLooping.EnableBeatLoop</c>.</summary>
    public string Mode { get; set; } = "beat-snap";

    /// <summary>Crossfade length at the loop wrap, in milliseconds. The default is 256 frames
    /// (about 5.3 ms at 48 kHz): short enough that transients aren't smeared, long enough to
    /// hide spectral mismatch across the seam. Clamped to 0.5–100 ms where it is used.</summary>
    public double FadeMs { get; set; } = 256 / 48.0;
}
